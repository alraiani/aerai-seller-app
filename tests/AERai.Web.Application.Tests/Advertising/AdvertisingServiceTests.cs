using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Advertising;

namespace AERai.Web.Application.Tests.Advertising;

public sealed class AdvertisingServiceTests
{
    private static readonly DateOnly From = new(2026, 10, 1);
    private static readonly DateOnly To = new(2026, 10, 30);

    private readonly FakeAdvertisingQueries _queries = new();

    private static AdSkuRow Sku(string sku, int? familyId, string? family, decimal spend, decimal adSales = 0m, long clicks = 0) =>
        new(sku, "B0" + sku, "Title " + sku, null, familyId, family, 1, AdMetrics.Zero with { Spend = spend, AdSales = adSales, Clicks = clicks, Impressions = clicks * 10 });

    private Task<AdvertisingReport> ReportAsync(int? familyId = null, string? search = null, AdSort sort = AdSort.Spend, bool descending = false) =>
        new AdvertisingService(_queries).GetReportAsync(
            TestMarketplaces.UnitedStates, new AdvertisingFilter(From, To, familyId, search, sort, descending), CancellationToken.None);

    [Fact]
    public async Task GetReportAsync_GroupsByFamilyNameWithSkusWithoutAFamilyLast()
    {
        _queries.Rows.AddRange(
        [
            Sku("LOOSE", null, null, 5m),
            Sku("MM-1", 1, "Microfiber Magic", 10m),
            Sku("HK-1", 2, "Handkerchiefs", 3m),
            Sku("MM-2", 1, "Microfiber Magic", 20m),
        ]);

        var report = await ReportAsync();

        Assert.Equal(["Handkerchiefs", "Microfiber Magic", null], report.Groups.Select(g => g.Family));
        var microfiber = report.Groups[1];
        Assert.Equal(["MM-2", "MM-1"], microfiber.Skus.Select(s => s.Sku));
        Assert.Equal(30m, microfiber.Totals.Spend);
        Assert.Equal(38m, report.Totals.Spend);
        Assert.Equal(4, report.SkuCount);
    }

    [Fact]
    public async Task GetReportAsync_RatiosComeFromTheSumsAndTotalSalesFromOrdersOverTheSameLocalDays()
    {
        _queries.Rows.AddRange([Sku("A", 1, "F", 10m, adSales: 40m, clicks: 20), Sku("B", 1, "F", 30m, adSales: 60m, clicks: 10)]);
        _queries.Sales["a"] = (4, 100m);
        _queries.Sales["B"] = (6, 300m);

        var report = await ReportAsync();

        var group = Assert.Single(report.Groups);
        Assert.Equal((40m, 100m, 400m), (group.Totals.Spend, group.Totals.AdSales, group.Totals.TotalSales));
        Assert.Equal(0.4m, group.Totals.Acos);
        Assert.Equal(0.1m, group.Totals.Tacos);
        Assert.Equal(40m / 30, group.Totals.CostPerClick);
        Assert.Equal(0.25m, group.Skus.Single(s => s.Sku == "A").Metrics.Acos);

        // 1 October 00:00 in New York, to the start of 31 October.
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 4, 0, 0, TimeSpan.Zero), _queries.SalesFrom);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 4, 0, 0, TimeSpan.Zero), _queries.SalesBefore);
    }

    [Fact]
    public async Task GetReportAsync_WithoutSalesOrClicks_RatiosAreUnknownNotZero()
    {
        _queries.Rows.Add(Sku("A", null, null, 5m));

        var metrics = (await ReportAsync()).Totals;

        Assert.Null(metrics.Acos);
        Assert.Null(metrics.Tacos);
        Assert.Null(metrics.CostPerClick);
        Assert.Null(metrics.ClickThroughRate);
    }

    [Fact]
    public async Task GetReportAsync_SortByAcos_PutsSkusWithoutAdSalesLastEitherWay()
    {
        _queries.Rows.AddRange([Sku("NONE", 1, "F", 5m), Sku("LOW", 1, "F", 1m, adSales: 10m), Sku("HIGH", 1, "F", 9m, adSales: 10m)]);

        var highestFirst = await ReportAsync(sort: AdSort.Acos);
        var lowestFirst = await ReportAsync(sort: AdSort.Acos, descending: true);

        Assert.Equal(["HIGH", "LOW", "NONE"], highestFirst.Groups[0].Skus.Select(s => s.Sku));
        Assert.Equal(["LOW", "HIGH", "NONE"], lowestFirst.Groups[0].Skus.Select(s => s.Sku));
    }

    [Theory]
    [InlineData(1, new[] { "MM-1" })]
    [InlineData(AdvertisingFilter.NoFamily, new[] { "LOOSE" })]
    public async Task GetReportAsync_FamilyFilter_KeepsOnlyThatFamily(int familyId, string[] expected)
    {
        _queries.Rows.AddRange([Sku("MM-1", 1, "Microfiber Magic", 1m), Sku("HK-1", 2, "Handkerchiefs", 1m), Sku("LOOSE", null, null, 1m)]);

        var report = await ReportAsync(familyId: familyId);

        Assert.Equal(expected, report.Groups.SelectMany(g => g.Skus).Select(s => s.Sku));
    }

    [Fact]
    public async Task GetReportAsync_Search_EveryWordMustMatchSomethingAboutTheSku()
    {
        _queries.Rows.AddRange([Sku("MM-1", 1, "Microfiber Magic", 1m), Sku("MM-2", 1, "Microfiber Magic", 1m), Sku("HK-1", 2, "Handkerchiefs", 1m)]);

        var report = await ReportAsync(search: "microfiber mm-2");

        Assert.Equal("MM-2", Assert.Single(Assert.Single(report.Groups).Skus).Sku);
    }

    [Fact]
    public async Task GetReportAsync_RangeLongerThanAYear_KeepsTheLatestDays()
    {
        var report = await new AdvertisingService(_queries).GetReportAsync(
            TestMarketplaces.UnitedStates, new AdvertisingFilter(new DateOnly(2020, 1, 1), To, null, null, AdSort.Spend, false), CancellationToken.None);

        Assert.Equal(To.AddDays(-(AdvertisingService.MaxDays - 1)), report.From);
        Assert.Equal((report.From, To), (_queries.SpendFrom, _queries.SpendTo));
    }

    private sealed class FakeAdvertisingQueries : IAdvertisingQueries
    {
        public List<AdSkuRow> Rows { get; } = [];

        public Dictionary<string, (int Units, decimal Sales)> Sales { get; } = new(StringComparer.OrdinalIgnoreCase);

        public DateOnly SpendFrom { get; private set; }

        public DateOnly SpendTo { get; private set; }

        public DateTimeOffset SalesFrom { get; private set; }

        public DateTimeOffset SalesBefore { get; private set; }

        public Task<IReadOnlyList<AdSkuRow>> GetSkuSpendAsync(string marketplaceId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            (SpendFrom, SpendTo) = (from, to);
            return Task.FromResult<IReadOnlyList<AdSkuRow>>(Rows);
        }

        public Task<IReadOnlyDictionary<string, (int Units, decimal Sales)>> GetSalesBySkuAsync(
            string marketplaceId, DateTimeOffset from, DateTimeOffset before, IReadOnlyCollection<string> skus, CancellationToken cancellationToken)
        {
            (SalesFrom, SalesBefore) = (from, before);
            return Task.FromResult<IReadOnlyDictionary<string, (int Units, decimal Sales)>>(Sales);
        }

        public Task<DateOnly?> GetLatestSpendDateAsync(string marketplaceId, CancellationToken cancellationToken) =>
            Task.FromResult<DateOnly?>(Rows.Count > 0 ? To : null);
    }
}
