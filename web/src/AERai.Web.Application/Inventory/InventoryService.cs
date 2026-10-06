using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Reporting;
using Microsoft.Extensions.Options;

namespace AERai.Web.Application.Inventory;

/// <summary>
/// Default <see cref="IInventoryService"/>: combines each SKU's latest stock with its sales history
/// to estimate how fast it sells and how many days its stock lasts.
/// </summary>
/// <remarks>
/// <para>
/// Velocity follows the desktop app's replenishment rules so both apps agree: units sold over the
/// last <see cref="VelocityWindowDays"/> local days, divided by the days the SKU's sales history
/// actually covers (from its first sale in the window to today). With fewer than
/// <see cref="MinCoverageDays"/> days of history or fewer than <see cref="MinUnitsSold"/> units sold,
/// velocity is unknown rather than a misleadingly precise guess.
/// </para>
/// <para>
/// Each SKU also gets a <see cref="RestockPlan"/> (see <see cref="RestockPlanner"/>) from its lead
/// times, which fall back to <see cref="InventoryOptions"/> where the SKU has none, and a
/// <see cref="StockStatus"/>. "Most urgent" means status first, then the soonest restock action,
/// then the fewest days of inventory.
/// </para>
/// <para>
/// Days are the marketplace's local days (<see cref="Marketplace.TimeZoneId"/>). Search, sorting,
/// and paging run in memory after the computation, because the sort key (days of inventory) is
/// computed here and a marketplace holds a catalog-sized number of SKUs, not millions.
/// </para>
/// </remarks>
/// <param name="queries">Inventory read queries.</param>
/// <param name="options">Restock-planning defaults.</param>
/// <param name="clock">Clock that defines "today".</param>
public sealed class InventoryService(IInventoryQueries queries, IOptions<InventoryOptions> options, TimeProvider clock) : IInventoryService
{
    /// <summary>Sales history used to estimate velocity, in local days including today.</summary>
    public const int VelocityWindowDays = 90;

    /// <summary>Fewest days of sales history for a velocity estimate.</summary>
    public const int MinCoverageDays = 14;

    /// <summary>Fewest units sold in the window for a velocity estimate.</summary>
    public const int MinUnitsSold = 5;

    private const int RecentSalesDays = 30;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InventoryItem>> GetItemsAsync(Marketplace marketplace, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var zone = TimeZoneInfo.FindSystemTimeZoneById(marketplace.TimeZoneId);
        var today = LocalTime.DateOf(clock.GetUtcNow(), zone);
        var windowStart = today.AddDays(-(VelocityWindowDays - 1));
        var recentStart = today.AddDays(-(RecentSalesDays - 1));

        var positions = await queries.GetPositionsAsync(marketplace.MarketplaceId, cancellationToken).ConfigureAwait(false);
        var sold = await queries.GetUnitsSoldAsync(marketplace.MarketplaceId, LocalTime.StartOfDay(windowStart, zone), cancellationToken).ConfigureAwait(false);
        var leadTimes = await queries.GetLeadTimesAsync(marketplace.MarketplaceId, cancellationToken).ConfigureAwait(false);
        var settings = options.Value;

        var salesBySku = sold
            .Select(s => (s.Sku, Day: LocalTime.DateOf(s.PurchaseDate, zone), s.Quantity))
            .Where(s => s.Day >= windowStart && s.Day <= today)
            .ToLookup(s => s.Sku, StringComparer.Ordinal);

        return positions
            .Select(p =>
            {
                var sales = salesBySku[p.Sku].ToList();
                var units90 = sales.Sum(s => s.Quantity);
                var units30 = sales.Where(s => s.Day >= recentStart).Sum(s => s.Quantity);
                var velocity = Velocity(sales.Select(s => s.Day).DefaultIfEmpty().Min(), units90, today);
                var times = settings.Resolve(leadTimes.GetValueOrDefault(p.Sku));
                var plan = RestockPlanner.Plan(today, velocity, p.SellThroughStock, p.HomeStock, times);
                var status = StatusOf(p, units30, velocity, plan, settings.AlertLeadDays);
                return new InventoryItem(p, units30, units90, velocity, DaysOfInventory(p, velocity), times, plan, status);
            })
            .OrderBy(i => i.Status)
            .ThenBy(i => i.Restock?.DaysUntilAction is null)
            .ThenBy(i => i.Restock?.DaysUntilAction)
            .ThenBy(i => i.DaysOfInventory is null)
            .ThenBy(i => i.DaysOfInventory)
            .ThenBy(i => i.Sku, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<InventoryOverview> GetOverviewAsync(Marketplace marketplace, PageRequest request, InventoryFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filter);

        var items = await GetItemsAsync(marketplace, cancellationToken).ConfigureAwait(false);
        var scoped = items
            .Where(i => filter.FamilyId is null || i.Position.FamilyId == filter.FamilyId)
            .Where(i => request.SafeSearch is not { } search || Matches(i.Position, search))
            .ToList();

        var matching = Sort(scoped.Where(i => InGroup(i.Status, filter.Status)), filter.Sort, filter.Descending).ToList();
        var page = matching.Skip(request.Skip).Take(request.SafePageSize).ToList();
        return new InventoryOverview(
            Totals(scoped),
            new PagedResult<InventoryItem>(page, matching.Count, request.SafePage, request.SafePageSize));
    }

    /// <summary>The SKU's stock state; the order of the checks is the order of urgency.</summary>
    private static StockStatus StatusOf(InventoryPosition position, int unitsSold30d, decimal? velocity, RestockPlan? plan, int soonDays) =>
        position.SnapshotDate is null ? StockStatus.NotAtAmazon
        : position.Available == 0 && unitsSold30d > 0 ? StockStatus.OutOfStock
        : plan?.DaysUntilAction is < 0 ? StockStatus.RestockOverdue
        : plan?.DaysUntilAction is { } due && due <= soonDays ? StockStatus.RestockSoon
        : velocity is null ? StockStatus.NoSalesData
        : StockStatus.Healthy;

    private static bool InGroup(StockStatus status, StockStatusFilter group) => group switch
    {
        StockStatusFilter.All => true,
        StockStatusFilter.NeedsAction => status is StockStatus.RestockOverdue or StockStatus.RestockSoon,
        StockStatusFilter.OutOfStock => status == StockStatus.OutOfStock,
        StockStatusFilter.Healthy => status == StockStatus.Healthy,
        StockStatusFilter.NoSalesData => status == StockStatus.NoSalesData,
        StockStatusFilter.NotAtAmazon => status == StockStatus.NotAtAmazon,
        _ => true,
    };

    /// <summary>
    /// Orders items. Each sort has a natural direction (most urgent, fewest days, best sellers, most
    /// stock, A→Z); <paramref name="descending"/> reverses it. Unknown values stay last either way.
    /// </summary>
    private static IEnumerable<InventoryItem> Sort(IEnumerable<InventoryItem> items, InventorySort sort, bool descending)
    {
        // Items arrive in urgency order, so that sort is just the input (or its reverse).
        if (sort == InventorySort.Urgency)
        {
            return descending ? items.Reverse() : items;
        }

        var ordered = sort switch
        {
            InventorySort.Sku => descending
                ? items.OrderByDescending(i => i.Sku, StringComparer.Ordinal)
                : items.OrderBy(i => i.Sku, StringComparer.Ordinal),
            InventorySort.DaysOfInventory => descending
                ? items.OrderBy(i => i.DaysOfInventory is null).ThenByDescending(i => i.DaysOfInventory)
                : items.OrderBy(i => i.DaysOfInventory is null).ThenBy(i => i.DaysOfInventory),
            InventorySort.Sold30d => descending ? items.OrderBy(i => i.UnitsSold30d) : items.OrderByDescending(i => i.UnitsSold30d),
            InventorySort.Sold90d => descending ? items.OrderBy(i => i.UnitsSold90d) : items.OrderByDescending(i => i.UnitsSold90d),
            InventorySort.Available => descending ? items.OrderBy(i => i.Position.Available) : items.OrderByDescending(i => i.Position.Available),
            _ => items.OrderBy(i => 0),
        };
        return ordered.ThenBy(i => i.Sku, StringComparer.Ordinal);
    }

    /// <summary>Average units per day over the days the SKU's sales history covers, or null when too thin.</summary>
    private static decimal? Velocity(DateOnly firstSale, int unitsSold, DateOnly today)
    {
        if (unitsSold < MinUnitsSold)
        {
            return null;
        }

        var coverageDays = Math.Min(VelocityWindowDays, today.DayNumber - firstSale.DayNumber + 1);
        return coverageDays < MinCoverageDays ? null : Math.Round((decimal)unitsSold / coverageDays, 2);
    }

    private static decimal? DaysOfInventory(InventoryPosition position, decimal? velocity) =>
        velocity is { } perDay and > 0 ? Math.Round(position.SellThroughStock / perDay, 1) : null;

    private static bool Matches(InventoryPosition p, string search) =>
        p.Sku.Contains(search, StringComparison.OrdinalIgnoreCase)
        || (p.Asin?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
        || (p.Title?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
        || (p.Family?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);

    private static InventoryTotals Totals(List<InventoryItem> items) => new(
        SkuCount: items.Count,
        Available: items.Sum(i => i.Position.Available),
        Inbound: items.Sum(i => i.Position.Inbound),
        Reserved: items.Sum(i => i.Position.Reserved),
        Unfulfillable: items.Sum(i => i.Position.Unfulfillable),
        HomeStock: items.Sum(i => i.Position.HomeStock),
        OutOfStock: items.Count(i => i.Status == StockStatus.OutOfStock),
        RestockOverdue: items.Count(i => i.Status == StockStatus.RestockOverdue),
        RestockSoon: items.Count(i => i.Status == StockStatus.RestockSoon),
        SnapshotDate: items.Count == 0 ? null : items.Max(i => i.Position.SnapshotDate));
}
