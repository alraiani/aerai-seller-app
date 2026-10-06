using System.Globalization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Domain.Reporting;
using Microsoft.Extensions.Options;

namespace AERai.Web.Application.Dashboard;

/// <summary>
/// Default <see cref="IDashboardService"/>: turns raw reporting rows into headline metrics, a chart,
/// best sellers, health glances, and a prioritized "needs attention" list.
/// </summary>
/// <remarks>
/// Days and hours are the marketplace's local ones (<see cref="Marketplace.TimeZoneId"/>), not
/// UTC, so an order placed at 11 PM Eastern counts on that day. Comparisons always use the same
/// amount of elapsed time ("today so far" vs "yesterday up to now"), so a partial day is never
/// compared with a full one.
/// </remarks>
/// <param name="queries">Dashboard read queries.</param>
/// <param name="options">Dashboard settings.</param>
/// <param name="clock">Clock that defines "now".</param>
public sealed class DashboardService(IDashboardQueries queries, IOptions<DashboardOptions> options, TimeProvider clock) : IDashboardService
{
    /// <summary>Velocity window used by the inventory view; "selling" SKUs sold within it.</summary>
    private const int SellingWindowDays = 30;

    /// <summary>How many SKUs to name in an attention item before summarizing the rest.</summary>
    private const int NamedSkuLimit = 3;

    /// <inheritdoc/>
    public async Task<DashboardSnapshot> GetSnapshotAsync(Marketplace marketplace, DashboardPeriod period, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var settings = options.Value;
        var id = marketplace.MarketplaceId;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(marketplace.TimeZoneId);
        var now = clock.GetUtcNow();
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var days = (int)period;

        // Current window: local midnight N-1 days ago → now. Comparison: the same span shifted back N days.
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var firstDay = today.AddDays(-(days - 1));
        var start = LocalInstant(firstDay.ToDateTime(TimeOnly.MinValue), zone);
        var previousStart = LocalInstant(firstDay.AddDays(-days).ToDateTime(TimeOnly.MinValue), zone);
        var previousEnd = LocalInstant(localNow.DateTime.AddDays(-days), zone);

        var lines = await queries.GetSalesLinesAsync(id, previousStart, now, cancellationToken).ConfigureAwait(false);
        var current = lines.Where(l => l.PurchaseDate >= start).ToList();
        var previous = lines.Where(l => l.PurchaseDate < previousEnd).ToList();

        var positions = await queries.GetInventoryPositionsAsync(id, cancellationToken).ConfigureAwait(false);
        var settlement = await queries.GetLatestSettlementAsync(id, cancellationToken).ConfigureAwait(false);
        var missingCost = await queries.GetSoldSkusMissingCostAsync(id, now.AddDays(-SellingWindowDays), cancellationToken).ConfigureAwait(false);
        var awaitingPromotion = await queries.CountBatchesAwaitingPromotionAsync(id, cancellationToken).ConfigureAwait(false);
        var sync = await queries.GetSyncHealthAsync(id, cancellationToken).ConfigureAwait(false);
        var syncPaused = await queries.IsSyncPausedAsync(cancellationToken).ConfigureAwait(false);
        var coverageStart = await queries.GetOrdersCoverageStartAsync(id, cancellationToken).ConfigureAwait(false);

        // A comparison is only meaningful if synced order history covers the whole comparison window;
        // otherwise a full period would be compared with a partly empty one (e.g. "+400%").
        var comparable = coverageStart is { } covered && covered <= previousStart;
        if (!comparable)
        {
            previous = [];
        }

        var revenue = new Metric(Revenue(current), Revenue(previous));
        var orders = new Metric(OrderCount(current), OrderCount(previous));
        var granularity = period == DashboardPeriod.Today ? ChartGranularity.Hour : ChartGranularity.Day;
        var inventory = Summarize(positions, settings.AtRiskDaysOfSupply);

        return new DashboardSnapshot(
            Period: period,
            TimeZoneId: marketplace.TimeZoneId,
            Currency: marketplace.Currency,
            GeneratedAt: now,
            WindowStart: TimeZoneInfo.ConvertTime(start, zone),
            Revenue: revenue,
            Orders: orders,
            Units: new Metric(current.Sum(l => l.Quantity), previous.Sum(l => l.Quantity)),
            AverageOrderValue: new Metric(
                orders.Current == 0 ? 0 : revenue.Current / orders.Current,
                orders.Previous == 0 ? 0 : revenue.Previous / orders.Previous),
            PendingOrders: current.Where(l => l.OrderStatus == "Pending").Select(l => l.AmazonOrderId).Distinct(StringComparer.Ordinal).Count(),
            Granularity: granularity,
            Chart: BuildChart(current, granularity, firstDay, days, localNow, zone),
            TopProducts: TopProducts(current, previous, settings.TopProductCount),
            Attention: BuildAttention(positions, inventory, sync, syncPaused, missingCost, awaitingPromotion, coverageStart, start, comparable, now, settings, zone),
            Inventory: inventory,
            LatestPayout: settlement is null
                ? null
                : new PayoutGlance(settlement.SettlementId, settlement.PeriodStart, settlement.PeriodEnd, settlement.Net, settlement.Sales, settlement.Fees, settlement.Refunds, marketplace.Currency),
            Sync: sync,
            SalesCoverageStart: coverageStart,
            ComparisonAvailable: comparable);
    }

    private static decimal Revenue(IEnumerable<SalesLine> lines) => lines.Sum(l => l.ItemPrice);

    private static int OrderCount(IEnumerable<SalesLine> lines) =>
        lines.Select(l => l.AmazonOrderId).Distinct(StringComparer.Ordinal).Count();

    /// <summary>One bucket per local hour (Today) or local day, including empty buckets so gaps show.</summary>
    private static List<ChartPoint> BuildChart(
        List<SalesLine> lines, ChartGranularity granularity, DateOnly firstDay, int days, DateTimeOffset localNow, TimeZoneInfo zone)
    {
        var local = lines.Select(l => (Line: l, Local: TimeZoneInfo.ConvertTime(l.PurchaseDate, zone))).ToList();

        if (granularity == ChartGranularity.Hour)
        {
            var byHour = local.ToLookup(x => x.Local.Hour);
            return Enumerable.Range(0, 24)
                .Select(hour => Point(
                    LocalInstant(firstDay.ToDateTime(new TimeOnly(hour, 0)), zone, toLocal: true),
                    byHour[hour].Select(x => x.Line),
                    isFuture: hour > localNow.Hour))
                .ToList();
        }

        var byDay = local.ToLookup(x => DateOnly.FromDateTime(x.Local.DateTime));
        return Enumerable.Range(0, days)
            .Select(offset => firstDay.AddDays(offset))
            .Select(day => Point(LocalInstant(day.ToDateTime(TimeOnly.MinValue), zone, toLocal: true), byDay[day].Select(x => x.Line), isFuture: false))
            .ToList();

        static ChartPoint Point(DateTimeOffset start, IEnumerable<SalesLine> bucket, bool isFuture)
        {
            var items = bucket.ToList();
            return new ChartPoint(start, Revenue(items), items.Sum(l => l.Quantity), OrderCount(items), isFuture);
        }
    }

    private static List<TopProduct> TopProducts(List<SalesLine> current, List<SalesLine> previous, int count)
    {
        var total = Revenue(current);
        var previousBySku = previous.GroupBy(l => l.Sku, StringComparer.Ordinal).ToDictionary(g => g.Key, g => Revenue(g), StringComparer.Ordinal);

        return current
            .GroupBy(l => l.Sku, StringComparer.Ordinal)
            .Select(g =>
            {
                var revenue = Revenue(g);
                var before = previousBySku.GetValueOrDefault(g.Key);
                return new TopProduct(
                    g.Key,
                    g.Select(l => l.Title).FirstOrDefault(t => t is not null),
                    g.Sum(l => l.Quantity),
                    revenue,
                    total == 0 ? 0 : revenue / total,
                    before == 0 ? null : (revenue - before) / before);
            })
            .OrderByDescending(p => p.Revenue)
            .ThenBy(p => p.Sku, StringComparer.Ordinal)
            .Take(count)
            .ToList();
    }

    private static InventoryGlance Summarize(IReadOnlyList<InventoryPosition> positions, decimal lowStockDays) => new(
        SkusTotal: positions.Count,
        SkusInStock: positions.Count(p => p.Available > 0),
        AvailableUnits: positions.Sum(p => p.Available),
        InboundUnits: positions.Sum(p => p.Inbound),
        OutOfStockSelling: positions.Count(IsOutOfStockSelling),
        LowStock: positions.Count(p => IsLowStock(p, lowStockDays)),
        SnapshotDate: positions.Count == 0 ? null : positions.Max(p => p.SnapshotDate));

    private static bool IsOutOfStockSelling(InventoryPosition p) => p.Available == 0 && p.UnitsSold30d > 0;

    private static bool IsLowStock(InventoryPosition p, decimal lowStockDays) =>
        p.Available > 0 && p.DaysOfSupply is { } days && days <= lowStockDays;

    /// <summary>
    /// Builds the "needs attention" list, most urgent first. Each rule produces at most one item so
    /// the list stays short enough to read at a glance.
    /// </summary>
    private static List<AttentionItem> BuildAttention(
        IReadOnlyList<InventoryPosition> positions,
        InventoryGlance inventory,
        IReadOnlyList<SyncGlance> sync,
        bool syncPaused,
        IReadOnlyList<string> missingCost,
        int awaitingPromotion,
        DateTimeOffset? coverageStart,
        DateTimeOffset windowStart,
        bool comparable,
        DateTimeOffset now,
        DashboardOptions settings,
        TimeZoneInfo zone)
    {
        var items = new List<AttentionItem>();

        foreach (var failed in sync.Where(s => s.LastStatus == SyncRunStatus.Failed))
        {
            items.Add(new AttentionItem(AttentionSeverity.Critical, $"{Label(failed.ReportType)} sync failed",
                Truncate(failed.LastMessage ?? "See run history for details.", 140), AttentionTarget.Sync));
        }

        if (inventory.OutOfStockSelling > 0)
        {
            var skus = positions.Where(IsOutOfStockSelling).OrderByDescending(p => p.UnitsSold30d).Select(p => p.Sku).ToList();
            items.Add(new AttentionItem(AttentionSeverity.Critical, Plural(inventory.OutOfStockSelling, "SKU") + " out of stock",
                "Selling recently but nothing available: " + NameList(skus), AttentionTarget.Inventory));
        }

        if (syncPaused)
        {
            items.Add(new AttentionItem(AttentionSeverity.Warning, "Amazon syncs are paused",
                "Scheduled pulls won't run until an operator resumes them.", AttentionTarget.Sync));
        }

        var orders = sync.FirstOrDefault(s => s.ReportType == AmazonReportType.Orders);
        if (orders?.LastSuccessAt is null)
        {
            items.Add(new AttentionItem(AttentionSeverity.Warning, "No sales data yet", "Run the Orders sync to load your sales.", AttentionTarget.Sync));
        }
        else if (now - orders.LastSuccessAt.Value > TimeSpan.FromHours(settings.StaleAfterHours))
        {
            var hours = (int)(now - orders.LastSuccessAt.Value).TotalHours;
            items.Add(new AttentionItem(AttentionSeverity.Warning, $"Sales data is {hours} hours old",
                orders.IsScheduled ? "The Orders schedule hasn't succeeded recently." : "Turn on the Orders schedule to keep sales current.", AttentionTarget.Sync));
        }

        if (inventory.LowStock > 0)
        {
            var skus = positions.Where(p => IsLowStock(p, settings.AtRiskDaysOfSupply)).OrderBy(p => p.DaysOfSupply).Select(p => p.Sku).ToList();
            items.Add(new AttentionItem(AttentionSeverity.Warning,
                $"{Plural(inventory.LowStock, "SKU")} under {settings.AtRiskDaysOfSupply:0} days of stock", "Reorder soon: " + NameList(skus), AttentionTarget.Inventory));
        }

        if (awaitingPromotion > 0)
        {
            items.Add(new AttentionItem(AttentionSeverity.Warning, $"{Plural(awaitingPromotion, "import batch", "import batches")} awaiting review",
                "Staged data isn't in your reports until it's promoted.", AttentionTarget.Batches));
        }

        if (missingCost.Count > 0)
        {
            items.Add(new AttentionItem(AttentionSeverity.Info, $"{Plural(missingCost.Count, "selling SKU")} without a cost",
                "Add cost of goods to see profit: " + NameList(missingCost), AttentionTarget.Products));
        }

        if (coverageStart is { } covered && covered > windowStart)
        {
            var localCovered = TimeZoneInfo.ConvertTime(covered, zone);
            items.Add(new AttentionItem(AttentionSeverity.Info,
                $"Sales before {localCovered.ToString("MMM d", CultureInfo.InvariantCulture)} not synced yet",
                "Totals for this period are incomplete. Backfill Orders to load up to 30 days.", AttentionTarget.Sync));
        }
        else if (!comparable && coverageStart is not null)
        {
            items.Add(new AttentionItem(AttentionSeverity.Info, "Backfill Orders to compare with earlier periods",
                "Synced history doesn't reach back far enough to show changes for this period yet.", AttentionTarget.Sync));
        }

        return items.OrderBy(i => i.Severity).ToList();
    }

    private static string Label(AmazonReportType type) => type switch
    {
        AmazonReportType.FbaInventory => "FBA inventory",
        _ => type.ToString(),
    };

    private static string Plural(int count, string singular, string? plural = null) =>
        count == 1 ? $"1 {singular}" : $"{count:N0} {plural ?? singular + "s"}";

    private static string NameList(IReadOnlyList<string> skus) =>
        skus.Count <= NamedSkuLimit
            ? string.Join(", ", skus)
            : $"{string.Join(", ", skus.Take(NamedSkuLimit))} +{skus.Count - NamedSkuLimit} more";

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";

    /// <summary>
    /// Converts a local wall-clock time to an instant. Times that fall in a daylight-saving gap move
    /// forward an hour; repeated (fall-back) times take their first occurrence.
    /// </summary>
    /// <param name="localWallTime">Local date and time.</param>
    /// <param name="zone">Business time zone.</param>
    /// <param name="toLocal">Return the instant expressed with the local offset (for display).</param>
    private static DateTimeOffset LocalInstant(DateTime localWallTime, TimeZoneInfo zone, bool toLocal = false)
    {
        var wall = DateTime.SpecifyKind(localWallTime, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(wall))
        {
            wall = wall.AddHours(1);
        }

        var offset = zone.IsAmbiguousTime(wall) ? zone.GetAmbiguousTimeOffsets(wall).Max() : zone.GetUtcOffset(wall);
        var instant = new DateTimeOffset(wall, offset);
        return toLocal ? instant : instant.ToUniversalTime();
    }
}
