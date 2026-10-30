using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Products;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Inventory;

/// <summary>Default <see cref="IInboundShipmentService"/>.</summary>
/// <param name="repository">Inbound shipment persistence.</param>
/// <param name="ledger">Home-stock ledger: an accepted shipment is logged through its Shipped to Amazon path.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class InboundShipmentService(
    IInboundShipmentRepository repository,
    IHomeStockLedgerService ledger,
    TimeProvider clock,
    ILogger<InboundShipmentService> logger) : IInboundShipmentService
{
    /// <summary>Most shipments the page lists: months of sends, newest first.</summary>
    public const int MaxListed = 300;

    /// <inheritdoc/>
    public async Task<InboundShipmentList> ListAsync(string marketplaceId, InboundShipmentFilter filter, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(filter);

        var shipments = await repository.ListAsync(marketplaceId, MaxListed, cancellationToken).ConfigureAwait(false);
        var rows = await ToRowsAsync(marketplaceId, shipments, cancellationToken).ConfigureAwait(false);

        // A few hundred shipments at most, so filtering in memory keeps the counts simple.
        var terms = ProductSearch.Terms(filter.Search);
        var scoped = rows
            .Where(r => terms.Count == 0 || terms.All(t => Matches(r, t)))
            .Where(r => filter.FamilyId is null || r.Lines.Any(l => l.FamilyId == filter.FamilyId))
            .ToList();

        var shown = filter.View switch
        {
            InboundShipmentView.ToReview => scoped.Where(r => r.IsToReview).ToList(),
            InboundShipmentView.Logged => scoped.Where(IsLogged).ToList(),
            InboundShipmentView.SetAside => scoped.Where(IsSetAside).ToList(),
            _ => scoped,
        };
        return new InboundShipmentList(shown, scoped.Count(r => r.IsToReview), scoped.Count(IsLogged), scoped.Count(IsSetAside), scoped.Count);
    }

    /// <inheritdoc/>
    public Task<int> CountToReviewAsync(string marketplaceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        return repository.CountToReviewAsync(marketplaceId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<HomeStockCountResult>> AcceptAsync(string marketplaceId, long id, IReadOnlyList<HomeStockShipment> lines, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        var shipment = await repository.GetAsync(marketplaceId, id, cancellationToken).ConfigureAwait(false);
        if (shipment is null)
        {
            return Result.Failure<HomeStockCountResult>("That shipment no longer exists.");
        }

        if (shipment.Review != InboundShipmentReview.ToReview)
        {
            return Result.Failure<HomeStockCountResult>($"Shipment {shipment.ShipmentConfirmationId} is no longer waiting for review.");
        }

        var reference = shipment.ShipmentConfirmationId;
        var logged = (await repository.GetLoggedUnitsAsync(marketplaceId, [reference], cancellationToken).ConfigureAwait(false))
            .GetValueOrDefault(reference) ?? new Dictionary<string, int>();
        var info = await repository.GetSkuInfoAsync(marketplaceId, shipment.Items.Select(i => i.Sku).ToList(), cancellationToken).ConfigureAwait(false);
        var fromHome = shipment.Items
            .Where(i => info.TryGetValue(i.Sku, out var sku) && sku.ShipsFromHomeStock)
            .ToDictionary(i => i.Sku, i => info[i.Sku].Sku, StringComparer.OrdinalIgnoreCase);

        // Only SKUs from home stock can be logged, and a SKU already logged under this FBA id never twice (a second
        // click, or a send logged on the Worksheet with the id). The catalog's spelling of the SKU is what the ledger keys on.
        var sends = lines
            .Where(l => l.Units > 0 && fromHome.ContainsKey(l.Sku) && !logged.ContainsKey(l.Sku))
            .GroupBy(l => l.Sku, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last() with { Sku = fromHome[g.Key] })
            .ToList();
        if (sends.Count == 0)
        {
            if (fromHome.Count > 0 && fromHome.Keys.All(logged.ContainsKey))
            {
                await repository.SetReviewAsync(marketplaceId, id, InboundShipmentReview.ToReview, InboundShipmentReview.Logged, clock.GetUtcNow(), user, cancellationToken).ConfigureAwait(false);
                return Result.Success(new HomeStockCountResult(0, 0, 0, []));
            }

            return Result.Failure<HomeStockCountResult>("Enter the units sent from home for at least one SKU, or dismiss the shipment if none came from home.");
        }

        var result = await ledger.ShipToAmazonAsync(marketplaceId, sends, reference, user, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result;
        }

        // A SKU whose home stock moved since the page loaded wasn't logged, so the shipment stays to review for it.
        if (result.Value.Stale.Count == 0)
        {
            await repository.SetReviewAsync(marketplaceId, id, InboundShipmentReview.ToReview, InboundShipmentReview.Logged, clock.GetUtcNow(), user, cancellationToken).ConfigureAwait(false);
        }

        LogAccepted(reference, result.Value.Applied, result.Value.UnitsOut, marketplaceId);
        return result;
    }

    /// <inheritdoc/>
    public async Task<Result> DismissAsync(string marketplaceId, long id, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        return await repository.SetReviewAsync(marketplaceId, id, InboundShipmentReview.ToReview, InboundShipmentReview.Dismissed, clock.GetUtcNow(), user, cancellationToken).ConfigureAwait(false)
            ? Result.Success()
            : Result.Failure("That shipment is no longer waiting for review.");
    }

    /// <inheritdoc/>
    public async Task<Result> ReopenAsync(string marketplaceId, long id, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        // Only a dismissed shipment comes back: one Amazon cancelled will never ship, and one logged is undone on the ledger.
        return await repository.SetReviewAsync(marketplaceId, id, InboundShipmentReview.Dismissed, InboundShipmentReview.ToReview, clock.GetUtcNow(), user, cancellationToken).ConfigureAwait(false)
            ? Result.Success()
            : Result.Failure("Only a dismissed shipment can be put back to review.");
    }

    /// <summary>Adds the catalog, home stock, and logged units to each shipment.</summary>
    private async Task<List<InboundShipmentRow>> ToRowsAsync(string marketplaceId, IReadOnlyList<InboundShipment> shipments, CancellationToken cancellationToken)
    {
        var skus = shipments.SelectMany(s => s.Items).Select(i => i.Sku).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var info = await repository.GetSkuInfoAsync(marketplaceId, skus, cancellationToken).ConfigureAwait(false);
        var logged = await repository.GetLoggedUnitsAsync(marketplaceId, shipments.Select(s => s.ShipmentConfirmationId).ToList(), cancellationToken).ConfigureAwait(false);
        return shipments.Select(s => ToRow(s, info, logged.GetValueOrDefault(s.ShipmentConfirmationId))).ToList();
    }

    /// <summary>One shipment as the page shows it.</summary>
    internal static InboundShipmentRow ToRow(InboundShipment shipment, IReadOnlyDictionary<string, InboundSkuInfo> info, IReadOnlyDictionary<string, int>? logged)
    {
        var toReview = shipment.Review == InboundShipmentReview.ToReview;
        var lines = shipment.Items
            .Select(i =>
            {
                var sku = info.GetValueOrDefault(i.Sku);
                var fromHome = sku?.ShipsFromHomeStock == true;
                var home = sku?.HomeStock ?? 0;
                var already = logged?.GetValueOrDefault(i.Sku) ?? 0;
                var suggested = toReview && fromHome && already == 0 ? Math.Clamp(i.Quantity, 0, Math.Max(0, home)) : 0;
                return new InboundShipmentLine(sku?.Sku ?? i.Sku, sku?.Title, sku?.FamilyId, sku?.FamilyName, fromHome, i.Quantity, home, already, suggested);
            })
            .OrderByDescending(l => l.FromHome)
            .ThenBy(l => l.Sku, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new InboundShipmentRow(
            shipment.Id, shipment.ShipmentConfirmationId, shipment.PlanName, shipment.Name, shipment.AmazonStatus,
            shipment.PlanCreatedAt, shipment.FirstSeenAt, shipment.Review, shipment.ReviewedAt, shipment.ReviewedBy, lines);
    }

    private static bool IsLogged(InboundShipmentRow row) =>
        row.Review is InboundShipmentReview.Logged or InboundShipmentReview.AlreadyLogged;

    private static bool IsSetAside(InboundShipmentRow row) =>
        row.Review is InboundShipmentReview.Dismissed or InboundShipmentReview.CancelledByAmazon
        || (row.Review == InboundShipmentReview.ToReview && !row.HasHomeStock);

    private static bool Matches(InboundShipmentRow row, string term) =>
        row.ShipmentConfirmationId.Contains(term, StringComparison.OrdinalIgnoreCase)
        || (row.PlanName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || (row.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || row.Lines.Any(l => l.Sku.Contains(term, StringComparison.OrdinalIgnoreCase) || (l.Title?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));

    [LoggerMessage(Level = LogLevel.Information, Message = "Accepted Amazon shipment {ShipmentId}: {Entries} sends, {Units} units from home stock in {MarketplaceId}")]
    private partial void LogAccepted(string shipmentId, int entries, int units, string marketplaceId);
}
