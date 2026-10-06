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
/// times, which fall back to <see cref="InventoryOptions"/> where the SKU has none. "Most urgent"
/// means the soonest restock action, then the fewest days of inventory.
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
                return new InventoryItem(p, units30, units90, velocity, DaysOfInventory(p, velocity), times, plan);
            })
            .OrderBy(i => i.Restock?.DaysUntilAction is null)
            .ThenBy(i => i.Restock?.DaysUntilAction)
            .ThenBy(i => i.DaysOfInventory is null)
            .ThenBy(i => i.DaysOfInventory)
            .ThenBy(i => i.Sku, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<InventoryOverview> GetOverviewAsync(Marketplace marketplace, PageRequest request, int? familyId, InventorySort sort, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var items = await GetItemsAsync(marketplace, cancellationToken).ConfigureAwait(false);
        var filtered = items
            .Where(i => familyId is null || i.Position.FamilyId == familyId)
            .Where(i => request.SafeSearch is not { } search || Matches(i.Position, search));

        // Items arrive in urgency order; the SKU order is for working down a list while counting.
        var matching = (sort == InventorySort.Sku ? filtered.OrderBy(i => i.Sku, StringComparer.Ordinal) : filtered).ToList();

        var page = matching.Skip(request.Skip).Take(request.SafePageSize).ToList();
        return new InventoryOverview(
            Totals(matching),
            new PagedResult<InventoryItem>(page, matching.Count, request.SafePage, request.SafePageSize));
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
        SnapshotDate: items.Count == 0 ? null : items.Max(i => i.Position.SnapshotDate));
}
