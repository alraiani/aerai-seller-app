using AERai.Web.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AERai.Web.Application.Dashboard;

/// <summary>
/// Default <see cref="IDashboardService"/>: aggregates reporting-view rows into KPIs, a trend, and alerts.
/// </summary>
/// <param name="reporting">Reporting-view queries.</param>
/// <param name="batches">Import batch queries.</param>
/// <param name="options">Dashboard tunables.</param>
/// <param name="timeProvider">Clock that defines "today" (UTC).</param>
public sealed class DashboardService(
    IReportingQueries reporting,
    IImportBatchQueries batches,
    IOptions<DashboardOptions> options,
    TimeProvider timeProvider) : IDashboardService
{
    /// <summary>Length of each window in the week-over-week comparison.</summary>
    private const int WeekDays = 7;

    /// <inheritdoc/>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        // Reporting views bucket sales by UTC date, so "today" must be the UTC date too.
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // One query covers both the trend window and the two weeks of the week-over-week comparison.
        var windowDays = Math.Max(settings.TrendDays, WeekDays * 2);
        var from = today.AddDays(-(windowDays - 1));

        var sales = await reporting.GetDailySalesAsync(from, today, cancellationToken).ConfigureAwait(false);
        var atRisk = await reporting.GetAtRiskInventoryAsync(settings.AtRiskDaysOfSupply, settings.AtRiskTake, cancellationToken).ConfigureAwait(false);
        var latestBatch = await batches.GetLatestAsync(cancellationToken).ConfigureAwait(false);

        var totalsByDate = sales
            .GroupBy(s => s.SalesDate)
            .ToDictionary(g => g.Key, g => new DailyTotal(g.Key, g.Sum(s => s.Units), g.Sum(s => s.Revenue)));

        var trend = Enumerable.Range(0, settings.TrendDays)
            .Select(offset => today.AddDays(offset - (settings.TrendDays - 1)))
            .Select(date => totalsByDate.TryGetValue(date, out var total) ? total : new DailyTotal(date, 0, 0m))
            .ToList();

        var todayBySku = sales
            .Where(s => s.SalesDate == today)
            .Select(s => new SkuSales(s.Sku, s.Title, s.Units, s.Revenue))
            .OrderByDescending(s => s.Revenue)
            .ThenBy(s => s.Sku, StringComparer.Ordinal)
            .ToList();

        return new DashboardSnapshot(
            Today: today,
            TodayUnits: todayBySku.Sum(s => s.Units),
            TodayRevenue: todayBySku.Sum(s => s.Revenue),
            Last7DaysRevenue: SumRevenue(totalsByDate, today.AddDays(-(WeekDays - 1)), today),
            Prior7DaysRevenue: SumRevenue(totalsByDate, today.AddDays(-(WeekDays * 2 - 1)), today.AddDays(-WeekDays)),
            TodayBySku: todayBySku,
            Trend: trend,
            AtRisk: atRisk,
            AtRiskThresholdDays: settings.AtRiskDaysOfSupply,
            LatestBatch: latestBatch);
    }

    /// <summary>Sums revenue for an inclusive date range.</summary>
    private static decimal SumRevenue(Dictionary<DateOnly, DailyTotal> totals, DateOnly from, DateOnly to) =>
        totals.Values.Where(t => t.Date >= from && t.Date <= to).Sum(t => t.Revenue);
}
