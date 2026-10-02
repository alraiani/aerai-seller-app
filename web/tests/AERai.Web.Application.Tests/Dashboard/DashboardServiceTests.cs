using AERai.Web.Application.Dashboard;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Reporting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Dashboard;

public sealed class DashboardServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 2);

    private readonly FakeReportingQueries _reporting = new();
    private readonly FakeImportBatchQueries _batches = new();

    // 23:30 UTC: late enough that a local-time "today" would differ in many US time zones.
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 2, 23, 30, 0, TimeSpan.Zero));

    private DashboardService CreateService(DashboardOptions? options = null) =>
        new(_reporting, _batches, Options.Create(options ?? new DashboardOptions { TrendDays = 14 }), _clock);

    private void AddSale(DateOnly date, string sku, int units, decimal revenue) =>
        _reporting.DailySales.Add(new DailySalesBySku { SalesDate = date, Sku = sku, Units = units, Revenue = revenue, OrderCount = 1 });

    [Fact]
    public async Task GetSnapshotAsync_TodaySales_AggregatesBySkuHighestRevenueFirst()
    {
        AddSale(Today, "A", 1, 10m);
        AddSale(Today, "B", 2, 40m);
        AddSale(Today.AddDays(-1), "A", 5, 50m);

        var snapshot = await CreateService().GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(Today, snapshot.Today);
        Assert.Equal(3, snapshot.TodayUnits);
        Assert.Equal(50m, snapshot.TodayRevenue);
        Assert.Equal(["B", "A"], snapshot.TodayBySku.Select(s => s.Sku));
    }

    [Fact]
    public async Task GetSnapshotAsync_GapsInSales_TrendIsZeroFilledAndEndsToday()
    {
        AddSale(Today.AddDays(-3), "A", 2, 20m);

        var snapshot = await CreateService().GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(14, snapshot.Trend.Count);
        Assert.Equal(Today.AddDays(-13), snapshot.Trend[0].Date);
        Assert.Equal(Today, snapshot.Trend[^1].Date);
        Assert.Equal(20m, snapshot.Trend.Single(t => t.Date == Today.AddDays(-3)).Revenue);
        Assert.Equal(13, snapshot.Trend.Count(t => t.Revenue == 0));
    }

    [Fact]
    public async Task GetSnapshotAsync_TwoWeeksOfSales_ComputesWeekOverWeekChange()
    {
        AddSale(Today, "A", 1, 150m);               // last 7 days
        AddSale(Today.AddDays(-6), "A", 1, 50m);    // last 7 days (boundary)
        AddSale(Today.AddDays(-7), "A", 1, 100m);   // prior 7 days (boundary)
        AddSale(Today.AddDays(-14), "A", 1, 999m);  // outside both windows

        var snapshot = await CreateService().GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(200m, snapshot.Last7DaysRevenue);
        Assert.Equal(100m, snapshot.Prior7DaysRevenue);
        Assert.Equal(1m, snapshot.WeekOverWeekChange);
    }

    [Fact]
    public async Task GetSnapshotAsync_NoPriorWeekRevenue_WeekOverWeekIsNull()
    {
        AddSale(Today, "A", 1, 10m);

        var snapshot = await CreateService().GetSnapshotAsync(CancellationToken.None);

        Assert.Null(snapshot.WeekOverWeekChange);
    }

    [Fact]
    public async Task GetSnapshotAsync_AtRiskThreshold_ReturnsOnlyPositionsAtOrBelowIt()
    {
        _reporting.Positions.Add(new InventoryPosition { Sku = "LOW", DaysOfSupply = 5 });
        _reporting.Positions.Add(new InventoryPosition { Sku = "EDGE", DaysOfSupply = 21 });
        _reporting.Positions.Add(new InventoryPosition { Sku = "OK", DaysOfSupply = 60 });

        var snapshot = await CreateService(new DashboardOptions { AtRiskDaysOfSupply = 21 }).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(["LOW", "EDGE"], snapshot.AtRisk.Select(p => p.Sku));
        Assert.Equal(21m, snapshot.AtRiskThresholdDays);
    }
}
