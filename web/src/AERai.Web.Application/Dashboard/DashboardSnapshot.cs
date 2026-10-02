using AERai.Web.Application.Imports;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Dashboard;

/// <summary>
/// Everything the dashboard page renders, computed in one call.
/// </summary>
/// <param name="Today">The UTC date treated as "today".</param>
/// <param name="TodayUnits">Units sold today.</param>
/// <param name="TodayRevenue">Revenue today.</param>
/// <param name="Last7DaysRevenue">Revenue for the 7 days ending today.</param>
/// <param name="Prior7DaysRevenue">Revenue for the 7 days before that.</param>
/// <param name="TodayBySku">Today's sales per SKU, highest revenue first.</param>
/// <param name="Trend">Daily totals for the trend window, oldest first, with zero-filled gaps.</param>
/// <param name="AtRisk">SKUs at or below the at-risk days-of-supply threshold.</param>
/// <param name="AtRiskThresholdDays">The threshold used for <paramref name="AtRisk"/>.</param>
/// <param name="LatestBatch">Most recent import, if any.</param>
public sealed record DashboardSnapshot(
    DateOnly Today,
    int TodayUnits,
    decimal TodayRevenue,
    decimal Last7DaysRevenue,
    decimal Prior7DaysRevenue,
    IReadOnlyList<SkuSales> TodayBySku,
    IReadOnlyList<DailyTotal> Trend,
    IReadOnlyList<InventoryPosition> AtRisk,
    decimal AtRiskThresholdDays,
    ImportBatchSummary? LatestBatch)
{
    /// <summary>
    /// Week-over-week revenue change as a fraction (0.25 = +25%); <see langword="null"/> when the prior week had no revenue.
    /// </summary>
    public decimal? WeekOverWeekChange => Prior7DaysRevenue == 0
        ? null
        : (Last7DaysRevenue - Prior7DaysRevenue) / Prior7DaysRevenue;
}
