using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Profit;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Tests.Profit;

/// <summary>Profit &amp; loss rules: columns, ads, cost of goods, refunds, spreads, filters, and reconciliation.</summary>
public sealed class ProfitServiceTests
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1);
    private static readonly DateOnly Oct31 = new(2026, 10, 31);

    private readonly FakeProfitQueries _queries = new();

    private Task<ProfitReport> ReportAsync(ProfitFilter? filter = null) =>
        new ProfitService(_queries).GetReportAsync(TestMarketplaces.UnitedStates, filter ?? Filter(), CancellationToken.None);

    private static ProfitFilter Filter(DateOnly? from = null, DateOnly? to = null) =>
        new(from ?? new DateOnly(2026, 8, 1), to ?? Oct31, null, null, ProfitShow.All, ProfitSort.Profit, false, false);

    private void Amount(DateOnly day, string? sku, ProfitCategory category, decimal amount, bool held = false) =>
        _queries.Amounts.Add(new ProfitAmountRow(day, sku, category, held, amount));

    private void Product(string sku, int? familyId = null, string? family = null) =>
        _queries.Products.Add(new ProfitProductRow(sku, "B" + sku, sku + " title", familyId, family, null));

    [Fact]
    public async Task GetReportAsync_SortsAmountsIntoColumns_AndProfitAddsThemUp()
    {
        Product("A");
        Amount(Oct1, "A", ProfitCategory.Sales, 100m);
        Amount(Oct1, "A", ProfitCategory.RefundedSales, -20m);
        Amount(Oct1, "A", ProfitCategory.RefundedExpenses, 3m);
        Amount(Oct1, "A", ProfitCategory.Commission, -15m);
        Amount(Oct1, "A", ProfitCategory.FbaFees, -6m);
        Amount(Oct1, "A", ProfitCategory.Shipping, -1m);
        Amount(Oct1, "A", ProfitCategory.OtherExpenses, -2m);
        Amount(Oct1, "A", ProfitCategory.Reserve, -50m);
        Amount(Oct1, null, ProfitCategory.Transfer, -500m);
        _queries.Sold.Add(new ProfitSoldUnitsRow(Oct1, "A", 10m, 3));

        var report = await ReportAsync();

        var m = Assert.Single(report.Skus).Metrics;
        Assert.Equal((100m, -17m, -22m, -2m, -30m), (m.Sales, m.Refunds, m.AmazonFees, m.Other, m.Cogs));
        Assert.Equal(29m, m.Profit);
        Assert.Equal(0.29m, m.Margin);
        Assert.Empty(report.Unassigned); // Reserve and transfers are never profit.
        Assert.Equal(29m, report.Totals.Profit);
    }

    [Fact]
    public async Task GetReportAsync_FromTheFirstSpendDay_SpendReplacesBilledCharges()
    {
        Product("A");
        Product("B");
        _queries.FirstSpendDay = new DateOnly(2026, 9, 15);
        Amount(new DateOnly(2026, 9, 20), "A", ProfitCategory.Sales, 100m);
        Amount(new DateOnly(2026, 9, 20), null, ProfitCategory.Advertising, -80m); // pays for spend already counted
        _queries.Spend.Add(new ProfitAdSpendRow(new DateOnly(2026, 9, 20), "A", 12.34m));

        var report = await ReportAsync();

        Assert.Equal(-12.34m, report.Skus.Single(s => s.Product.Sku == "A").Metrics.Advertising);
        Assert.Equal(-12.34m, report.Totals.Advertising);
        Assert.False(report.Skus.Single().BilledAdvertising);
    }

    [Fact]
    public async Task GetReportAsync_BeforeSpendSynced_SpreadsBilledChargesByEachMonthsSales()
    {
        Product("A");
        Product("B");
        _queries.FirstSpendDay = new DateOnly(2026, 10, 1);
        Amount(new DateOnly(2026, 8, 3), "A", ProfitCategory.Sales, 30m);
        Amount(new DateOnly(2026, 8, 9), "B", ProfitCategory.Sales, 60m);
        Amount(new DateOnly(2026, 8, 31), null, ProfitCategory.Advertising, -10m);
        Amount(new DateOnly(2026, 9, 10), null, ProfitCategory.Advertising, -5m); // no sales that month

        var report = await ReportAsync();

        var bySku = report.Skus.ToDictionary(s => s.Product.Sku);
        Assert.Equal((-3.33m, -6.67m), (bySku["A"].Metrics.Advertising, bySku["B"].Metrics.Advertising));
        Assert.True(bySku["A"].BilledAdvertising);
        var notSplit = Assert.Single(report.Unassigned);
        Assert.Equal((ProfitUnassignedKind.AdvertisingNotSplit, -5m), (notSplit.Kind, notSplit.Metrics.Advertising));
        Assert.Equal(-15m, report.Totals.Advertising);
    }

    [Fact]
    public async Task GetReportAsync_CostByOrderDate_AndMissingCostCountsUnitsNotMoney()
    {
        Product("A");
        Amount(Oct1, "A", ProfitCategory.Sales, 50m);
        _queries.Sold.Add(new ProfitSoldUnitsRow(Oct1, "A", 4m, 2));
        _queries.Sold.Add(new ProfitSoldUnitsRow(Oct1, "A", 5m, 1));
        _queries.Sold.Add(new ProfitSoldUnitsRow(Oct1, "A", null, 4));

        var report = await ReportAsync();

        var m = report.Skus.Single().Metrics;
        Assert.Equal((7, -13m, 4), (m.Units, m.Cogs, m.CostMissingUnits));
        Assert.Equal(1, report.CostMissingSkus);
    }

    [Theory]
    [InlineData(-20.0, 40.0, 4, 2)]   // principal ÷ the order's price each
    [InlineData(-200.0, 40.0, 4, 4)]  // never more than the item's quantity
    [InlineData(-4.0, 40.0, 4, 0)]    // a partial refund under half a unit gives none back
    public async Task GetReportAsync_RefundedUnits_GiveTheirCostBack(double principal, double itemPrice, int quantity, int units)
    {
        Product("A");
        Amount(Oct1, "A", ProfitCategory.Sales, 40m);
        _queries.Sold.Add(new ProfitSoldUnitsRow(Oct1, "A", 3m, 4));
        _queries.Refunds.Add(new ProfitRefundRow(Oct1, "A", (decimal)principal, (decimal)itemPrice, quantity, 3m));

        var m = (await ReportAsync()).Skus.Single().Metrics;

        Assert.Equal(units, m.RefundedUnits);
        Assert.Equal(units * 3m, m.CostReturned);
        Assert.Equal(-12m + units * 3m, m.Cogs);
    }

    [Fact]
    public async Task GetReportAsync_RefundWithoutItsOrder_UsesTheSkusAverageSalePrice()
    {
        Product("A");
        Amount(Oct1, "A", ProfitCategory.Sales, 50m);
        _queries.Sold.Add(new ProfitSoldUnitsRow(Oct1, "A", 2m, 5));
        _queries.Refunds.Add(new ProfitRefundRow(Oct1, "A", -20m, null, null, 2m));

        var m = (await ReportAsync()).Skus.Single().Metrics;

        Assert.Equal((2, 4m), (m.RefundedUnits, m.CostReturned));
    }

    [Fact]
    public async Task GetReportAsync_FullyLoaded_SpreadsAccountAmountsAndKeepsTheTotal()
    {
        Product("A");
        Product("B");
        Amount(Oct1, "A", ProfitCategory.Sales, 10m);
        Amount(Oct1, "B", ProfitCategory.Sales, 20m);
        Amount(Oct1, null, ProfitCategory.OtherExpenses, -39.99m);

        var plain = await ReportAsync();
        var loaded = await ReportAsync(Filter() with { FullyLoaded = true });

        Assert.Equal(-39.99m, Assert.Single(plain.Unassigned).Metrics.Other);
        Assert.Empty(loaded.Unassigned);
        Assert.Equal([-13.33m, -26.66m], loaded.Skus.OrderBy(s => s.Product.Sku).Select(s => s.Metrics.Other));
        Assert.Equal(plain.Totals.Profit, loaded.Totals.Profit);
    }

    [Fact]
    public async Task GetReportAsync_NarrowingFilter_LeavesAccountAmountsOutOfTheTotals()
    {
        Product("A", 1, "Mats");
        Product("B");
        Amount(Oct1, "A", ProfitCategory.Sales, 10m);
        Amount(Oct1, "B", ProfitCategory.Sales, 20m);
        Amount(Oct1, null, ProfitCategory.OtherExpenses, -5m);

        var report = await ReportAsync(Filter() with { FamilyId = 1 });

        Assert.Equal("A", Assert.Single(report.Skus).Product.Sku);
        Assert.Empty(report.Unassigned);
        Assert.Equal(10m, report.Totals.Profit);
    }

    [Fact]
    public async Task GetReportAsync_FamiliesByNameWithNoFamilyLast_LosingMoneyFilterAndSearch()
    {
        Product("A", 2, "Towels");
        Product("B", 1, "Mats");
        Product("C");
        Amount(Oct1, "A", ProfitCategory.Sales, 10m);
        Amount(Oct1, "B", ProfitCategory.Sales, 10m);
        Amount(Oct1, "B", ProfitCategory.FbaFees, -15m);
        Amount(Oct1, "C", ProfitCategory.Sales, 5m);

        var all = await ReportAsync();
        var losing = await ReportAsync(Filter() with { Show = ProfitShow.LosingMoney });
        var searched = await ReportAsync(Filter() with { Search = "towels a" });

        Assert.Equal(["Mats", "Towels", null], all.Families.Select(f => f.Family));
        Assert.Equal("B", Assert.Single(losing.Skus).Product.Sku);
        Assert.Equal("A", Assert.Single(searched.Skus).Product.Sku);
    }

    [Fact]
    public async Task GetReportAsync_ShortRange_IsChartedByWeekFromMonday_LongByMonth()
    {
        Product("A");
        Amount(new DateOnly(2026, 10, 7), "A", ProfitCategory.Sales, 10m); // a Wednesday

        var weeks = await ReportAsync(Filter(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));
        var months = await ReportAsync();

        Assert.Equal(ProfitGranularity.Week, weeks.Granularity);
        Assert.Equal(new DateOnly(2026, 9, 28), weeks.Periods[0].Start);
        Assert.Equal(10m, weeks.Periods.Single(p => p.Start == new DateOnly(2026, 10, 5)).Metrics.Sales);
        Assert.Equal(ProfitGranularity.Month, months.Granularity);
        Assert.Equal([new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 1), Oct1], months.Periods.Select(p => p.Start));
        Assert.Equal(months.Totals.Profit, months.Periods.Sum(p => p.Metrics.Profit));
    }

    [Fact]
    public async Task GetReportAsync_HeldAmounts_AreCountedAndShown()
    {
        Product("A");
        Amount(Oct1, "A", ProfitCategory.Sales, 30m, held: true);
        Amount(Oct1, "A", ProfitCategory.Commission, -4.5m, held: true);
        Amount(Oct1, "A", ProfitCategory.Sales, 10m);

        var m = (await ReportAsync()).Skus.Single().Metrics;

        Assert.Equal((40m, 25.5m), (m.Sales, m.Held));
    }

    [Fact]
    public async Task GetReportAsync_MarginWithoutSales_IsUnknownAndSortsLast()
    {
        Product("A");
        Product("B");
        Amount(Oct1, "A", ProfitCategory.FbaFees, -3m);
        Amount(Oct1, "B", ProfitCategory.Sales, 10m);

        var report = await ReportAsync(Filter() with { Sort = ProfitSort.Margin });

        Assert.Null(report.Skus.Single(s => s.Product.Sku == "A").Metrics.Margin);
        Assert.Equal(["B", "A"], report.Skus.Select(s => s.Product.Sku));
    }

    [Fact]
    public async Task GetReconciliationAsync_TiesDiffersOrPredatesTheLedger_AndTheBridgeAddsUp()
    {
        Product("A");
        _queries.LedgerStart = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        _queries.FirstSpendDay = new DateOnly(2026, 8, 1);
        Amount(Oct1, "A", ProfitCategory.Sales, 200m);
        Amount(Oct1, "A", ProfitCategory.Commission, -30m);
        Amount(Oct1, null, ProfitCategory.Advertising, -25m);
        _queries.Spend.Add(new ProfitAdSpendRow(Oct1, "A", 20m));
        _queries.Sold.Add(new ProfitSoldUnitsRow(Oct1, "A", 10m, 5));
        _queries.Ties.Add(new SettlementTieRow("S3", new DateTimeOffset(2026, 10, 2, 2, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 16, 2, 0, 0, TimeSpan.Zero), 140m, -5m, 140m, 9));
        _queries.Ties.Add(new SettlementTieRow("S2", new DateTimeOffset(2026, 9, 18, 2, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 2, 2, 0, 0, TimeSpan.Zero), 10m, 0m, 9.99m, 3));
        _queries.Ties.Add(new SettlementTieRow("S1", new DateTimeOffset(2026, 8, 20, 2, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 3, 2, 0, 0, TimeSpan.Zero), 50m, 0m, null, 0));

        var result = await new ProfitService(_queries).GetReconciliationAsync(TestMarketplaces.UnitedStates, Oct1, Oct31, CancellationToken.None);

        Assert.Equal(
            [(SettlementTieState.Ties, (decimal?)0m), (SettlementTieState.Differs, 0.01m), (SettlementTieState.BeforeLedger, null)],
            result.Settlements.Select(s => (s.State, s.Difference)));
        var b = result.Bridge;
        Assert.Equal((200m, -5m, 5m, -50m), (b.Payouts, b.Reserve, b.AdvertisingAdjustment, b.Cogs));
        Assert.Equal(b.Profit, b.Payouts - b.Reserve + b.Timing + b.AdvertisingAdjustment + b.Cogs);
        Assert.Equal(100m, b.Profit); // 200 − 30 − 20 spend − 50 cost
    }

    [Fact]
    public async Task GetSkuAsync_ReturnsOneSkusPeriods_OrNullWithoutFigures()
    {
        Product("A");
        Amount(new DateOnly(2026, 9, 5), "a", ProfitCategory.Sales, 10m); // SKUs match ignoring case
        Amount(Oct1, "A", ProfitCategory.Sales, 7m);

        var service = new ProfitService(_queries);
        var sku = await service.GetSkuAsync(TestMarketplaces.UnitedStates, "A", new DateOnly(2026, 8, 1), Oct31, false, CancellationToken.None);
        var none = await service.GetSkuAsync(TestMarketplaces.UnitedStates, "Z", new DateOnly(2026, 8, 1), Oct31, false, CancellationToken.None);

        Assert.NotNull(sku);
        Assert.Equal(17m, sku.Sku.Metrics.Sales);
        Assert.Equal([0m, 10m, 7m], sku.Periods.Select(p => p.Metrics.Sales));
        Assert.Null(none);
    }

    [Fact]
    public void Range_ReversedOrTooLong_IsPutInOrderKeepingTheLatestDays()
    {
        Assert.Equal((Oct1, Oct31), ProfitService.Range(Oct31, Oct1));
        Assert.Equal((Oct31.AddDays(-(ProfitService.MaxDays - 1)), Oct31), ProfitService.Range(new DateOnly(2020, 1, 1), Oct31));
    }

    private sealed class FakeProfitQueries : IProfitQueries
    {
        public List<ProfitAmountRow> Amounts { get; } = [];

        public List<ProfitSoldUnitsRow> Sold { get; } = [];

        public List<ProfitRefundRow> Refunds { get; } = [];

        public List<ProfitAdSpendRow> Spend { get; } = [];

        public List<ProfitProductRow> Products { get; } = [];

        public List<SettlementTieRow> Ties { get; } = [];

        public DateOnly? FirstSpendDay { get; set; }

        public DateTimeOffset? LedgerStart { get; set; }

        public Task<IReadOnlyList<ProfitAmountRow>> GetAmountsAsync(string marketplaceId, DateOnly from, DateOnly to, string timeZoneId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProfitAmountRow>>([.. Amounts.Where(a => a.Day >= from && a.Day <= to)]);

        public Task<IReadOnlyList<ProfitSoldUnitsRow>> GetSoldUnitsAsync(string marketplaceId, DateOnly from, DateOnly to, string timeZoneId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProfitSoldUnitsRow>>([.. Sold.Where(a => a.Day >= from && a.Day <= to)]);

        public Task<IReadOnlyList<ProfitRefundRow>> GetRefundsAsync(string marketplaceId, DateOnly from, DateOnly to, string timeZoneId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProfitRefundRow>>([.. Refunds.Where(a => a.Day >= from && a.Day <= to)]);

        public Task<IReadOnlyList<ProfitAdSpendRow>> GetAdSpendAsync(string marketplaceId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProfitAdSpendRow>>([.. Spend.Where(a => a.Day >= from && a.Day <= to)]);

        public Task<DateOnly?> GetFirstAdSpendDayAsync(string marketplaceId, CancellationToken cancellationToken) => Task.FromResult(FirstSpendDay);

        public Task<DateTimeOffset?> GetLedgerStartAsync(string marketplaceId, CancellationToken cancellationToken) => Task.FromResult(LedgerStart);

        public Task<IReadOnlyList<ProfitProductRow>> GetProductsAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProfitProductRow>>([.. Products.Where(p => skus.Contains(p.Sku, StringComparer.OrdinalIgnoreCase))]);

        public Task<IReadOnlyList<SettlementTieRow>> GetSettlementTiesAsync(string marketplaceId, DateTimeOffset periodEndFrom, DateTimeOffset periodEndBefore, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SettlementTieRow>>(Ties);

        public Task<IReadOnlyList<ProfitUnmappedRow>> GetOtherExpenseKindsAsync(string marketplaceId, DateOnly from, DateOnly to, string timeZoneId, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProfitUnmappedRow>>([]);
    }
}
