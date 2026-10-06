using System.Globalization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Alerts;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Application.Alerts;

/// <summary>
/// Default <see cref="IStockAlertService"/>. Alert rules use the same numbers as the Inventory page
/// (<see cref="IInventoryService"/>), so an alert always matches what the page shows.
/// </summary>
/// <remarks>
/// <para>Rules, for SKUs that Amazon reports in the marketplace:</para>
/// <list type="bullet">
/// <item><b>Out</b>: nothing available and the SKU sold in the last 30 days.</item>
/// <item><b>Low</b>: still available, but its next restock action (send or order) is due within
/// <see cref="InventoryOptions.AlertLeadDays"/> days or is overdue.</item>
/// </list>
/// <para>Refreshing is idempotent: running it twice, or on two instances, ends in the same state.</para>
/// </remarks>
/// <param name="inventory">Inventory numbers and restock plans.</param>
/// <param name="repository">Alert persistence.</param>
/// <param name="options">Restock settings (the "soon" window).</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class StockAlertService(
    IInventoryService inventory,
    IStockAlertRepository repository,
    IOptions<InventoryOptions> options,
    TimeProvider clock,
    ILogger<StockAlertService> logger) : IStockAlertService
{
    /// <summary>How long resolved alerts stay on the Notifications page.</summary>
    public static readonly TimeSpan ResolvedVisibleFor = TimeSpan.FromDays(7);

    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    /// <inheritdoc/>
    public async Task<StockAlertChanges> RefreshAsync(Marketplace marketplace, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var now = clock.GetUtcNow();
        var items = await inventory.GetItemsAsync(marketplace, cancellationToken).ConfigureAwait(false);
        var wanted = items
            .Select(i => (i.Sku, Alert: Evaluate(i, options.Value.AlertLeadDays)))
            .Where(x => x.Alert is not null)
            .ToDictionary(x => x.Sku, x => x.Alert!.Value, StringComparer.Ordinal); // Non-null: filtered above.

        var open = await repository.GetOpenAsync(marketplace.MarketplaceId, cancellationToken).ConfigureAwait(false);
        var raise = new List<StockAlert>();
        var resolve = new List<long>();
        var messages = new Dictionary<long, string>();

        foreach (var alert in open)
        {
            if (!wanted.TryGetValue(alert.Sku, out var desired) || desired.Level != alert.Level)
            {
                // A change of level closes the old alert; the new one below notifies users again.
                resolve.Add(alert.Id);
            }
            else if (desired.Message != alert.Message)
            {
                messages[alert.Id] = desired.Message;
            }
        }

        var stillOpen = open.Where(a => !resolve.Contains(a.Id)).Select(a => a.Sku).ToHashSet(StringComparer.Ordinal);
        foreach (var (sku, desired) in wanted.Where(w => !stillOpen.Contains(w.Key)))
        {
            raise.Add(new StockAlert
            {
                MarketplaceId = marketplace.MarketplaceId,
                Sku = sku,
                Level = desired.Level,
                Message = desired.Message,
                RaisedAt = now,
                UpdatedAt = now,
            });
        }

        var changes = new StockAlertChanges(raise, resolve, messages);
        if (changes.IsEmpty)
        {
            return changes;
        }

        if (!await repository.ApplyAsync(changes, now, cancellationToken).ConfigureAwait(false))
        {
            LogConflict(marketplace.Code);
            return new StockAlertChanges([], [], new Dictionary<long, string>());
        }

        LogRefreshed(marketplace.Code, raise.Count, resolve.Count);
        return changes;
    }

    /// <inheritdoc/>
    public Task<int> CountUnreadAsync(string marketplaceId, string userEmail, CancellationToken cancellationToken) =>
        repository.CountUnreadAsync(marketplaceId, userEmail, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StockAlertView>> ListAsync(string marketplaceId, string userEmail, CancellationToken cancellationToken)
    {
        var alerts = await repository.ListAsync(marketplaceId, userEmail, clock.GetUtcNow() - ResolvedVisibleFor, cancellationToken).ConfigureAwait(false);
        return alerts
            .OrderBy(a => a.ResolvedAt is not null)
            .ThenBy(a => a.IsRead)
            .ThenByDescending(a => a.Level)
            .ThenByDescending(a => a.ResolvedAt ?? a.RaisedAt)
            .ToList();
    }

    /// <inheritdoc/>
    public Task MarkReadAsync(string marketplaceId, long alertId, string userEmail, CancellationToken cancellationToken) =>
        repository.MarkReadAsync(marketplaceId, [alertId], userEmail, clock.GetUtcNow(), cancellationToken);

    /// <inheritdoc/>
    public Task<int> MarkAllReadAsync(string marketplaceId, string userEmail, CancellationToken cancellationToken) =>
        repository.MarkReadAsync(marketplaceId, null, userEmail, clock.GetUtcNow(), cancellationToken);

    /// <summary>The alert a SKU should have right now, or null when it is fine.</summary>
    private static (StockAlertLevel Level, string Message)? Evaluate(InventoryItem item, int soonDays)
    {
        // SKUs only held at home have nothing at Amazon to run out of yet.
        if (item.Position.SnapshotDate is null)
        {
            return null;
        }

        if (item.Position.Available == 0 && item.IsSelling)
        {
            return (StockAlertLevel.Out, Join(string.Create(En, $"Out of stock at Amazon; sold {item.UnitsSold30d:N0} in the last 30 days."), Actions(item.Restock)));
        }

        if (item.Position.Available > 0 && item.Restock?.DaysUntilAction is { } due && due <= soonDays)
        {
            var when = due switch
            {
                < 0 => $"{-due} day{(due == -1 ? "" : "s")} overdue",
                0 => "due today",
                _ => $"due in {due} day{(due == 1 ? "" : "s")}",
            };
            return (StockAlertLevel.Low, Join(string.Create(En, $"Restock {when}; about {item.DaysOfInventory:N0} days of stock left."), Actions(item.Restock)));
        }

        return null;
    }

    private static string? Actions(RestockPlan? plan)
    {
        if (plan is null)
        {
            return null;
        }

        var parts = new List<string>(2);
        if (plan.SendFromHome > 0)
        {
            parts.Add(string.Create(En, $"Send {plan.SendFromHome:N0} from home stock by {plan.SendBy:MMM d}"));
        }

        if (plan.OrderFromSupplier > 0)
        {
            parts.Add(string.Create(En, $"Order {plan.OrderFromSupplier:N0} by {plan.OrderBy:MMM d}"));
        }

        return parts.Count == 0 ? null : string.Join("; ", parts) + ".";
    }

    private static string Join(string first, string? second) => second is null ? first : $"{first} {second}";

    [LoggerMessage(Level = LogLevel.Information, Message = "Stock alerts for {Marketplace}: {Raised} raised, {Resolved} resolved")]
    private partial void LogRefreshed(string marketplace, int raised, int resolved);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stock alerts for {Marketplace} were refreshed concurrently elsewhere; skipped")]
    private partial void LogConflict(string marketplace);
}
