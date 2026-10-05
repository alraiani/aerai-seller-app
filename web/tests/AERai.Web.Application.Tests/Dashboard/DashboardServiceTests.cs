using AERai.Web.Application.Dashboard;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Domain.Reporting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Dashboard;

public sealed class DashboardServiceTests
{
    // Mon 2026-10-05 15:00 UTC = 11:00 EDT (UTC-4).
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 15, 0, 0, TimeSpan.Zero);

    private readonly FakeDashboardQueries _queries = new();
    private readonly FakeTimeProvider _clock = new(Now);

    public DashboardServiceTests()
    {
        // Healthy, fresh Orders sync by default so tests only see the attention items they set up.
        _queries.Sync.Add(new SyncGlance(AmazonReportType.Orders, true, Now.AddMinutes(-10), Now.AddMinutes(-10), SyncRunStatus.Succeeded, null));

        // History covers well over 60 days, so every comparison window is complete unless a test says otherwise.
        _queries.CoverageStart = Now.AddDays(-90);
    }

    private DashboardService CreateService(DashboardOptions? options = null) =>
        new(_queries, Options.Create(options ?? new DashboardOptions()), _clock);

    private void Sale(DateTimeOffset at, string sku, decimal price, int quantity = 1, string order = "", string status = "Shipped") =>
        _queries.Lines.Add(new SalesLine
        {
            AmazonOrderId = order.Length > 0 ? order : Guid.NewGuid().ToString("N"),
            PurchaseDate = at,
            OrderStatus = status,
            Currency = "USD",
            Sku = sku,
            Title = $"Title {sku}",
            Quantity = quantity,
            ItemPrice = price,
        });

    private static DateTimeOffset Eastern(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, TimeSpan.FromHours(-4));

    [Fact]
    public async Task Today_UsesLocalDayBoundaryNotUtc()
    {
        Sale(Eastern(10, 4, 23, 30), "LATE", 50m);   // 03:30 UTC Oct 5, but Oct 4 locally → yesterday
        Sale(Eastern(10, 5, 9, 15), "MORNING", 20m);  // today

        var snapshot = await CreateService().GetSnapshotAsync(DashboardPeriod.Today, CancellationToken.None);

        Assert.Equal(20m, snapshot.Revenue.Current);
        Assert.Equal(Eastern(10, 5, 0), snapshot.WindowStart);
    }

    [Fact]
    public async Task Today_ComparesWithYesterdayUpToTheSameTime()
    {
        Sale(Eastern(10, 5, 10), "A", 30m);
        Sale(Eastern(10, 4, 10), "A", 20m);  // yesterday before 11:00 → counted
        Sale(Eastern(10, 4, 18), "A", 500m); // yesterday after 11:00 → excluded from the comparison

        var snapshot = await CreateService().GetSnapshotAsync(DashboardPeriod.Today, CancellationToken.None);

        Assert.Equal(20m, snapshot.Revenue.Previous);
        Assert.Equal(0.5m, snapshot.Revenue.Change);
    }

    [Fact]
    public async Task Metrics_CountDistinctOrdersAndAverageOrderValue()
    {
        Sale(Eastern(10, 5, 8), "A", 10m, 2, order: "O1");
        Sale(Eastern(10, 5, 8), "B", 30m, 1, order: "O1");
        Sale(Eastern(10, 5, 9), "A", 20m, 1, order: "O2", status: "Pending");

        var snapshot = await CreateService().GetSnapshotAsync(DashboardPeriod.Today, CancellationToken.None);

        Assert.Equal(60m, snapshot.Revenue.Current);
        Assert.Equal(2m, snapshot.Orders.Current);
        Assert.Equal(4m, snapshot.Units.Current);
        Assert.Equal(30m, snapshot.AverageOrderValue.Current);
        Assert.Equal(1, snapshot.PendingOrders);
        Assert.Null(snapshot.Revenue.Change); // Nothing yesterday to compare against.
    }

    [Fact]
    public async Task Today_ChartHas24LocalHoursWithLaterHoursMarkedFuture()
    {
        Sale(Eastern(10, 5, 9, 30), "A", 15m);

        var snapshot = await CreateService().GetSnapshotAsync(DashboardPeriod.Today, CancellationToken.None);

        Assert.Equal(ChartGranularity.Hour, snapshot.Granularity);
        Assert.Equal(24, snapshot.Chart.Count);
        Assert.Equal(15m, snapshot.Chart[9].Revenue);
        Assert.False(snapshot.Chart[11].IsFuture); // 11:00 is the current hour.
        Assert.True(snapshot.Chart[12].IsFuture);
    }

    [Fact]
    public async Task SevenDays_HasOneBucketPerLocalDayAndFetchesTheComparisonWindow()
    {
        Sale(Eastern(9, 29, 12), "A", 10m);  // first day of the window
        Sale(Eastern(9, 28, 10), "A", 99m);  // day before, before 11:00 → comparison period only

        var snapshot = await CreateService().GetSnapshotAsync(DashboardPeriod.Last7Days, CancellationToken.None);

        Assert.Equal(7, snapshot.Chart.Count);
        Assert.Equal(new DateOnly(2026, 9, 29), DateOnly.FromDateTime(snapshot.Chart[0].LocalStart.DateTime));
        Assert.Equal(10m, snapshot.Revenue.Current);
        Assert.Equal(99m, snapshot.Revenue.Previous);
        Assert.Equal(Eastern(9, 22, 0).ToUniversalTime(), _queries.RequestedWindow!.Value.From);
    }

    [Fact]
    public async Task TopProducts_RankByRevenueWithShareAndChange()
    {
        Sale(Eastern(10, 5, 8), "BIG", 75m);
        Sale(Eastern(10, 5, 8), "SMALL", 25m);
        Sale(Eastern(10, 4, 8), "BIG", 50m);

        var snapshot = await CreateService().GetSnapshotAsync(DashboardPeriod.Today, CancellationToken.None);

        Assert.Equal(["BIG", "SMALL"], snapshot.TopProducts.Select(p => p.Sku));
        Assert.Equal(0.75m, snapshot.TopProducts[0].Share);
        Assert.Equal(0.5m, snapshot.TopProducts[0].Change);
        Assert.Null(snapshot.TopProducts[1].Change);
    }

    [Fact]
    public async Task Attention_AllHealthy_IsEmpty()
    {
        var snapshot = await CreateService().GetSnapshotAsync(DashboardPeriod.Last7Days, CancellationToken.None);

        Assert.Empty(snapshot.Attention);
    }

    [Fact]
    public async Task Attention_OrdersCriticalFirstAndNamesSkus()
    {
        _queries.MissingCost.AddRange(["C1", "C2", "C3", "C4"]);
        _queries.Positions.Add(new InventoryPosition { Sku = "OUT", Available = 0, UnitsSold30d = 12 });
        _queries.Positions.Add(new InventoryPosition { Sku = "LOW", Available = 10, UnitsSold30d = 30, DaysOfSupply = 10 });
        _queries.Positions.Add(new InventoryPosition { Sku = "DEAD", Available = 0, UnitsSold30d = 0 });
        _queries.AwaitingPromotion = 2;

        var snapshot = await CreateService().GetSnapshotAsync(DashboardPeriod.Last7Days, CancellationToken.None);

        Assert.Equal(
            [AttentionSeverity.Critical, AttentionSeverity.Warning, AttentionSeverity.Warning, AttentionSeverity.Info],
            snapshot.Attention.Select(a => a.Severity));
        Assert.Equal("1 SKU out of stock", snapshot.Attention[0].Title); // DEAD isn't selling, so it isn't urgent.
        Assert.Contains("OUT", snapshot.Attention[0].Detail, StringComparison.Ordinal);
        Assert.Contains("C1, C2, C3 +1 more", snapshot.Attention[3].Detail, StringComparison.Ordinal);
        Assert.Equal((1, 1), (snapshot.Inventory.OutOfStockSelling, snapshot.Inventory.LowStock));
    }

    [Fact]
    public async Task Attention_FailedSyncAndStaleSales()
    {
        _queries.Sync.Clear();
        _queries.Sync.Add(new SyncGlance(AmazonReportType.Orders, true, Now.AddHours(-30), Now.AddHours(-1), SyncRunStatus.Failed, "HTTP 403"));

        var snapshot = await CreateService().GetSnapshotAsync(DashboardPeriod.Last7Days, CancellationToken.None);

        Assert.Equal("Orders sync failed", snapshot.Attention[0].Title);
        Assert.Equal("HTTP 403", snapshot.Attention[0].Detail);
        Assert.Equal("Sales data is 30 hours old", snapshot.Attention[1].Title);
    }

    [Fact]
    public async Task Attention_PeriodStartsBeforeSyncedHistory_FlagsIncompleteTotals()
    {
        _queries.CoverageStart = Eastern(9, 28, 10).ToUniversalTime();

        var thirtyDays = await CreateService().GetSnapshotAsync(DashboardPeriod.Last30Days, CancellationToken.None);
        var today = await CreateService().GetSnapshotAsync(DashboardPeriod.Today, CancellationToken.None);

        Assert.Contains(thirtyDays.Attention, a => a.Title == "Sales before Sep 28 not synced yet");
        Assert.DoesNotContain(today.Attention, a => a.Title.StartsWith("Sales before", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ComparisonWindowNotFullySynced_ShowsNoChangeInsteadOfMisleadingPercentages()
    {
        // History starts Sep 28 10:00 local; the 7-day comparison window starts Sep 22.
        _queries.CoverageStart = Eastern(9, 28, 10).ToUniversalTime();
        Sale(Eastern(10, 5, 9), "A", 100m);
        Sale(Eastern(9, 28, 9), "A", 5m); // partial day inside the uncovered comparison window

        var snapshot = await CreateService().GetSnapshotAsync(DashboardPeriod.Last7Days, CancellationToken.None);

        Assert.False(snapshot.ComparisonAvailable);
        Assert.Contains(snapshot.Attention, a => a.Title == "Backfill Orders to compare with earlier periods");
        Assert.Null(snapshot.Revenue.Change);
        Assert.Null(snapshot.TopProducts[0].Change);
        Assert.Equal(100m, snapshot.Revenue.Current);
    }

    [Fact]
    public async Task LatestPayout_ComputesFeeRate()
    {
        _queries.LatestSettlement = new SettlementSummary { SettlementId = "S", Currency = "USD", Sales = 1000m, Fees = -400m, Refunds = -10m, Net = 590m };

        var snapshot = await CreateService().GetSnapshotAsync(DashboardPeriod.Last7Days, CancellationToken.None);

        Assert.Equal(590m, snapshot.LatestPayout!.Net);
        Assert.Equal(0.4m, snapshot.LatestPayout.FeeRate);
    }
}
