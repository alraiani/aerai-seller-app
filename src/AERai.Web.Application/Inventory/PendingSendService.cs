using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Application.Inventory;

/// <summary>Default <see cref="IPendingSendService"/>.</summary>
/// <param name="repository">Pending-send persistence.</param>
/// <param name="queries">Inventory reads (latest snapshots, sales).</param>
/// <param name="ledger">Home-stock ledger (cancelling reverses the send).</param>
/// <param name="refresh">Queues an inventory sync after a shipment id is added.</param>
/// <param name="alerts">Asks for a stock-alert refresh when pending units change (they feed the restock plan).</param>
/// <param name="options">Restock settings (when a pending send is overdue).</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class PendingSendService(
    IPendingSendRepository repository,
    IInventoryQueries queries,
    IHomeStockLedgerService ledger,
    IInventoryRefreshRequester refresh,
    IStockAlertRefreshSignal alerts,
    IOptions<InventoryOptions> options,
    TimeProvider clock,
    ILogger<PendingSendService> logger) : IPendingSendService
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<PendingSendItem>> ListAsync(string marketplaceId, int? familyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);

        var now = clock.GetUtcNow();
        var expiry = TimeSpan.FromDays(options.Value.PendingSendExpiryDays);
        return (await repository.ListOpenAsync(marketplaceId, cancellationToken).ConfigureAwait(false))
            .Where(s => familyId is null || s.FamilyId == familyId)
            .Select(s => new PendingSendItem(s, (int)(now - s.CreatedAt).TotalDays, now - s.CreatedAt > expiry))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<int> ReconcileAsync(string marketplaceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);

        var open = await repository.ListOpenAsync(marketplaceId, cancellationToken).ConfigureAwait(false);
        if (open.Count == 0)
        {
            return 0;
        }

        var stock = (await queries.GetPositionsAsync(marketplaceId, cancellationToken).ConfigureAwait(false))
            .Where(p => p.SnapshotReceivedAt is not null)
            .ToDictionary(p => p.Sku, p => new AmazonStockReading(p.SellThroughStock, p.Inbound, p.SnapshotReceivedAt!.Value), StringComparer.Ordinal); // Non-null: filtered.

        // Sends are compared from the oldest open one per SKU; nothing before it can change the result.
        var oldest = open
            .Where(s => stock.ContainsKey(s.Sku))
            .GroupBy(s => s.Sku, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Min(s => s.CreatedAt), StringComparer.Ordinal);
        if (oldest.Count == 0)
        {
            return 0;
        }

        var since = oldest.Values.Min();
        var sends = (await repository.ListSinceAsync(marketplaceId, oldest.Keys.ToList(), since, cancellationToken).ConfigureAwait(false))
            .ToLookup(s => s.Sku, StringComparer.Ordinal);

        // Sales are counted from the earliest baseline snapshot, which can be older than the send itself.
        var salesFrom = open.Select(s => s.AmazonBaseline?.ReceivedAt ?? s.CreatedAt).Append(since).Min();
        var sold = (await queries.GetUnitsSoldAsync(marketplaceId, salesFrom, cancellationToken).ConfigureAwait(false))
            .ToLookup(s => s.Sku, StringComparer.Ordinal);

        var matches = oldest
            .SelectMany(o => PendingReconciliation.Match(
                sends[o.Key].Where(s => s.CreatedAt >= o.Value).ToList(), stock[o.Key], sold[o.Key].ToList()))
            .ToList();
        if (matches.Count == 0)
        {
            return 0;
        }

        var updated = await repository.ReconcileAsync(marketplaceId, matches, clock.GetUtcNow(), HomeStockMovement.SystemReconciler, cancellationToken).ConfigureAwait(false);
        var closed = matches.Count(m => m.Closed);
        LogReconciled(updated, closed, marketplaceId);
        if (updated > 0)
        {
            alerts.Request();
        }

        return updated;
    }

    /// <inheritdoc/>
    public async Task<Result> MarkReconciledAsync(string marketplaceId, long id, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        if (await repository.GetAsync(marketplaceId, id, cancellationToken).ConfigureAwait(false) is not { IsOpen: true } send)
        {
            return Result.Failure("That send is no longer pending.");
        }

        var updated = await repository.ReconcileAsync(marketplaceId, [new PendingSendMatch(id, send.UnitsSent, Closed: true)], clock.GetUtcNow(), user, cancellationToken).ConfigureAwait(false);
        if (updated == 0)
        {
            return Result.Failure("That send is no longer pending.");
        }

        LogMarked(id, send.Sku, send.Pending, user, marketplaceId);
        alerts.Request();
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> CancelAsync(string marketplaceId, long id, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        if (await repository.GetAsync(marketplaceId, id, cancellationToken).ConfigureAwait(false) is not { IsOpen: true } send)
        {
            return Result.Failure("That send is no longer pending.");
        }

        // Units Amazon already shows did go out, so putting them all back at home would be wrong.
        if (send.ReconciledUnits > 0)
        {
            return Result.Failure($"Amazon already shows {send.ReconciledUnits:N0} of these units, so the send can't be cancelled. Mark it reconciled, then log a count correction for any units still at home.");
        }

        return await ledger.ReverseAsync(marketplaceId, id, user, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result> SetShipmentIdAsync(string marketplaceId, long id, string? shipmentId, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        var reference = shipmentId?.Trim();
        if (string.IsNullOrEmpty(reference))
        {
            return Result.Failure("Enter the FBA shipment id.");
        }

        string? error =
            reference.Length > HomeStockLedgerService.MaxReferenceLength ? $"The shipment id can be at most {HomeStockLedgerService.MaxReferenceLength} characters."
            : !PendingSend.IsShipmentId(reference) ? "FBA shipment ids start with FBA (for example FBA15K7Y3ZQX)."
            : null;
        if (error is not null)
        {
            return Result.Failure(error);
        }

        if (!await repository.SetReferenceAsync(marketplaceId, id, reference, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure("That send is no longer pending, or already has a reference.");
        }

        LogReferenceSet(id, reference, user, marketplaceId);
        await refresh.RequestAsync(marketplaceId, user, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reconciled pending sends in {MarketplaceId}: {Updated} updated, {Closed} closed")]
    private partial void LogReconciled(int updated, int closed, string marketplaceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Send {Id} ({Sku}, {Pending} pending) marked reconciled by {User} in {MarketplaceId}")]
    private partial void LogMarked(long id, string sku, int pending, string user, string marketplaceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Shipment id {Reference} added to send {Id} by {User} in {MarketplaceId}")]
    private partial void LogReferenceSet(long id, string reference, string user, string marketplaceId);
}
