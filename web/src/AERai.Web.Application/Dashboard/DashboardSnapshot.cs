namespace AERai.Web.Application.Dashboard;

/// <summary>
/// Everything the dashboard renders, computed in one call. All times are in
/// <see cref="TimeZoneId"/>; all money is in <see cref="Currency"/> only (never mixed).
/// </summary>
/// <param name="Period">Selected period.</param>
/// <param name="TimeZoneId">IANA time zone used for days and hours.</param>
/// <param name="Currency">Currency of every amount.</param>
/// <param name="GeneratedAt">When the snapshot was computed.</param>
/// <param name="WindowStart">Start of the selected period (local midnight).</param>
/// <param name="Revenue">Revenue (Pending included, Cancelled excluded).</param>
/// <param name="Orders">Distinct orders.</param>
/// <param name="Units">Units sold.</param>
/// <param name="AverageOrderValue">Revenue ÷ orders.</param>
/// <param name="PendingOrders">Orders in the period still Pending at Amazon.</param>
/// <param name="Granularity">Whether <paramref name="Chart"/> is hourly or daily.</param>
/// <param name="Chart">Sales chart buckets, oldest first.</param>
/// <param name="TopProducts">Best sellers by revenue.</param>
/// <param name="Attention">Items that need action, most urgent first; empty means all clear.</param>
/// <param name="Inventory">Inventory health.</param>
/// <param name="LatestPayout">Most recent settlement in <paramref name="Currency"/>, if any.</param>
/// <param name="Sync">Freshness per report type.</param>
/// <param name="SalesCoverageStart">Earliest point synced order history is known to cover.</param>
/// <param name="ComparisonAvailable">
/// Whether synced history covers the whole comparison window. When <see langword="false"/>, all
/// <see cref="Metric.Previous"/> values are 0 and every change is <see langword="null"/>.
/// </param>
public sealed record DashboardSnapshot(
    DashboardPeriod Period,
    string TimeZoneId,
    string Currency,
    DateTimeOffset GeneratedAt,
    DateTimeOffset WindowStart,
    Metric Revenue,
    Metric Orders,
    Metric Units,
    Metric AverageOrderValue,
    int PendingOrders,
    ChartGranularity Granularity,
    IReadOnlyList<ChartPoint> Chart,
    IReadOnlyList<TopProduct> TopProducts,
    IReadOnlyList<AttentionItem> Attention,
    InventoryGlance Inventory,
    PayoutGlance? LatestPayout,
    IReadOnlyList<SyncGlance> Sync,
    DateTimeOffset? SalesCoverageStart,
    bool ComparisonAvailable);
