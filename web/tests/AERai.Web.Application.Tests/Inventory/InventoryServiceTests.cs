using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Reporting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class InventoryServiceTests
{
    // Mon 2026-10-05 15:00 UTC = 11:00 EDT (UTC-4).
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 15, 0, 0, TimeSpan.Zero);

    private static readonly Marketplace Us = TestMarketplaces.UnitedStates;

    private readonly FakeInventoryQueries _queries = new();

    private readonly InventoryOptions _options = new();

    private InventoryService CreateService() => new(_queries, Options.Create(_options), new FakeTimeProvider(Now));

    private static DateTimeOffset Eastern(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, TimeSpan.FromHours(-4));

    private void Stock(string sku, int available = 0, Action<InventoryPosition>? configure = null)
    {
        var position = new InventoryPosition { MarketplaceId = Us.MarketplaceId, Sku = sku, Available = available, SnapshotDate = new DateOnly(2026, 10, 5) };
        configure?.Invoke(position);
        _queries.Positions.Add(position);
    }

    private void Sold(string sku, DateTimeOffset at, int quantity) => _queries.Sold.Add(new UnitsSold(sku, at, quantity));

    private async Task<InventoryItem> SingleItemAsync() => Assert.Single(await CreateService().GetItemsAsync(Us, CancellationToken.None));

    [Fact]
    public async Task GetItemsAsync_VelocityDividesByDaysSinceFirstSale()
    {
        Stock("A", available: 30, p => p.InboundShipped = 10);
        Sold("A", Eastern(9, 16, 10), 8); // 20 local days including today
        Sold("A", Eastern(10, 5, 9), 12);

        var item = await SingleItemAsync();

        Assert.Equal(1.00m, item.DailyVelocity);
        Assert.Equal(40.0m, item.DaysOfInventory); // (30 available + 10 inbound) / 1 per day
    }

    [Fact]
    public async Task GetItemsAsync_TooFewUnits_VelocityUnknown()
    {
        Stock("A", available: 30);
        Sold("A", Eastern(8, 1, 10), 4);

        var item = await SingleItemAsync();

        Assert.Equal(4, item.UnitsSold90d);
        Assert.Null(item.DailyVelocity);
        Assert.Null(item.DaysOfInventory);
    }

    [Fact]
    public async Task GetItemsAsync_TooShortHistory_VelocityUnknown()
    {
        Stock("A", available: 30);
        Sold("A", Eastern(9, 30, 10), 50); // 6 days of history

        Assert.Null((await SingleItemAsync()).DailyVelocity);
    }

    [Fact]
    public async Task GetItemsAsync_CountsSalesByLocalDay()
    {
        Stock("A");
        Sold("A", Eastern(9, 6, 23, 30), 1);  // first day of the 30-day window (Sep 6 local)
        Sold("A", Eastern(9, 5, 23, 30), 2);  // Sep 6 03:30 UTC, but Sep 5 locally: outside 30 days
        Sold("A", Eastern(7, 8, 12), 4);      // first day of the 90-day window
        Sold("A", Eastern(7, 7, 12), 100);    // too old for either window

        var item = await SingleItemAsync();

        Assert.Equal((1, 7), (item.UnitsSold30d, item.UnitsSold90d));
        Assert.Equal(Eastern(7, 8, 0).ToUniversalTime(), _queries.RequestedSince);
    }

    [Fact]
    public async Task GetItemsAsync_DaysOfInventoryIgnoresSoldAndUnsellableStock()
    {
        Stock("A", available: 10, p =>
        {
            p.ReservedFcTransfer = 5;
            p.ReservedFcProcessing = 5;
            p.ReservedCustomerOrder = 50; // already sold
            p.Unfulfillable = 50;         // never sells
        });
        Sold("A", Eastern(9, 6, 12), 30);

        var item = await SingleItemAsync();

        Assert.Equal(20.0m, item.DaysOfInventory); // (10 + 5 + 5) / 1 per day
    }

    [Fact]
    public async Task GetItemsAsync_SoonestActionFirstThenCoveredThenUnknownVelocity()
    {
        Stock("COVERED", available: 300);            // 300 days at 1/day: above the 90-day target
        Sold("COVERED", Eastern(9, 6, 12), 30);
        Stock("NEW", available: 1);                  // no sales history
        Stock("URGENT", available: 3);
        Sold("URGENT", Eastern(9, 6, 12), 30);
        Stock("LATER", available: 80);
        Sold("LATER", Eastern(9, 6, 12), 30);

        var items = await CreateService().GetItemsAsync(Us, CancellationToken.None);

        Assert.Equal(["URGENT", "LATER", "COVERED", "NEW"], items.Select(i => i.Sku));
    }

    [Fact]
    public async Task GetItemsAsync_UsesSkuLeadTimesWithDefaultsForBlankFields()
    {
        Stock("A", available: 30, p => p.HomeStock = 10);
        Sold("A", Eastern(9, 6, 12), 30); // 1 per day
        _queries.LeadTimes["A"] = new LeadTimeSettings(null, null, 5, 0, 60);

        var item = await SingleItemAsync();

        Assert.True(item.LeadTimes.IsCustom);
        Assert.Equal((_options.SupplierLeadTimeDays, 5, 0, 60), (item.LeadTimes.SupplierLeadTimeDays, item.LeadTimes.TransitDays, item.LeadTimes.SafetyStockDays, item.LeadTimes.TargetStockDays));
        Assert.NotNull(item.Restock);
        Assert.Equal((30, 10, 20), (item.Restock.UnitsNeeded, item.Restock.SendToAmazon, item.Restock.ShortAtHome));
    }

    [Fact]
    public async Task GetOverviewAsync_SearchMatchesAsinAndTotalsCoverAllMatches()
    {
        Stock("MAT-BLK", available: 10, p => { p.Asin = "B0MAT1"; p.InboundWorking = 4; p.ReservedUnsplit = 2; });
        Stock("MAT-BLU", available: 20, p => p.Asin = "B0MAT2");
        Stock("STRAP", available: 99, p => p.Asin = "B0STR1");

        var overview = await CreateService().GetOverviewAsync(Us, new PageRequest(1, 1, "b0mat"), InventoryFilter.Default, CancellationToken.None);

        Assert.Equal(2, overview.Items.TotalCount);
        Assert.Single(overview.Items.Items);
        Assert.Equal((30, 4, 2, 32, 36), (overview.Totals.Available, overview.Totals.Inbound, overview.Totals.Reserved, overview.Totals.InWarehouse, overview.Totals.AmazonTotal));
    }

    [Fact]
    public async Task GetOverviewAsync_AwdStock_AddsToOverallTotalButNotToFbaOrCover()
    {
        Stock("A", available: 30, p => { p.HomeStock = 5; p.AwdOnHand = 100; p.AwdInbound = 20; p.AwdReplenishment = 12; });
        Sold("A", Eastern(9, 16, 10), 20); // 1 per day over 20 local days

        var overview = await CreateService().GetOverviewAsync(Us, new PageRequest(1, 25, null), InventoryFilter.Default, CancellationToken.None);

        var t = overview.Totals;
        Assert.Equal((30, 100, 20, 120), (t.AmazonTotal, t.AwdOnHand, t.AwdInbound, t.AwdTotal));

        // To-FBA units are left out: they are likely already in FBA inbound.
        Assert.Equal(155, t.OverallTotal);
        var item = Assert.Single(overview.Items.Items);
        Assert.Equal(155, item.Position.OverallTotal);
        Assert.Equal(30.0m, item.DaysOfInventory); // AWD stock does not count as sell-through cover
    }

    [Fact]
    public async Task GetItemsAsync_AssignsOneStatusPerSku()
    {
        // 1 per day; default lead times: order 61 days ahead, send 24; alert window 7 days.
        Stock("OUT");
        Sold("OUT", Eastern(9, 6, 12), 30);
        Stock("OVERDUE", available: 10);
        Sold("OVERDUE", Eastern(9, 6, 12), 30);
        Stock("SOON", available: 66);           // order due in 5 days
        Sold("SOON", Eastern(9, 6, 12), 30);
        Stock("HEALTHY", available: 120);
        Sold("HEALTHY", Eastern(9, 6, 12), 30);
        Stock("NEW", available: 5);
        Stock("HOME", configure: p => { p.SnapshotDate = null; p.HomeStock = 9; });

        var items = await CreateService().GetItemsAsync(Us, CancellationToken.None);

        Assert.Equal(
            [("OUT", StockStatus.OutOfStock), ("OVERDUE", StockStatus.RestockOverdue), ("SOON", StockStatus.RestockSoon),
             ("HEALTHY", StockStatus.Healthy), ("NEW", StockStatus.NoSalesData), ("HOME", StockStatus.NotAtAmazon)],
            items.Select(i => (i.Sku, i.Status)));
    }

    [Fact]
    public async Task GetOverviewAsync_StatusFilterNarrowsRowsButNotTheCounts()
    {
        Stock("OUT");
        Sold("OUT", Eastern(9, 6, 12), 30);
        Stock("OVERDUE", available: 10);
        Sold("OVERDUE", Eastern(9, 6, 12), 30);
        Stock("SOON", available: 66);
        Sold("SOON", Eastern(9, 6, 12), 30);
        Stock("HEALTHY", available: 120);
        Sold("HEALTHY", Eastern(9, 6, 12), 30);

        var overview = await CreateService().GetOverviewAsync(Us, new PageRequest(), new InventoryFilter(Status: StockStatusFilter.NeedsAction), CancellationToken.None);

        Assert.Equal(["OVERDUE", "SOON"], overview.Items.Items.Select(i => i.Sku));
        Assert.Equal((4, 1, 1, 1, 2), (overview.Totals.SkuCount, overview.Totals.OutOfStock, overview.Totals.RestockOverdue, overview.Totals.RestockSoon, overview.Totals.NeedsAction));
    }

    [Theory]
    [InlineData(InventorySort.Sold30d, false, new[] { "FAST", "SLOW", "NONE" })]
    [InlineData(InventorySort.Sold30d, true, new[] { "NONE", "SLOW", "FAST" })]
    [InlineData(InventorySort.DaysOfInventory, false, new[] { "FAST", "SLOW", "NONE" })]
    [InlineData(InventorySort.Available, false, new[] { "NONE", "SLOW", "FAST" })]
    [InlineData(InventorySort.Sku, true, new[] { "SLOW", "NONE", "FAST" })]
    public async Task GetOverviewAsync_SortsEachWayWithUnknownsLast(InventorySort sort, bool descending, string[] expected)
    {
        Stock("FAST", available: 10);
        Sold("FAST", Eastern(9, 6, 12), 60);    // 2/day -> 5 days
        Stock("SLOW", available: 40);
        Sold("SLOW", Eastern(9, 6, 12), 30);    // 1/day -> 40 days
        Stock("NONE", available: 90);           // no sales: unknown cover

        var overview = await CreateService().GetOverviewAsync(Us, new PageRequest(), new InventoryFilter(Sort: sort, Descending: descending), CancellationToken.None);

        Assert.Equal(expected, overview.Items.Items.Select(i => i.Sku));
    }

    [Fact]
    public async Task GetItemsAsync_AveragesPerDayOverTheDaysWithHistoryInEachWindow()
    {
        Stock("A", available: 10);
        Sold("A", Eastern(9, 26, 12), 10);  // 10 local days ago (Sep 26 → Oct 5 = 10 days)
        Sold("A", Eastern(8, 7, 12), 60);   // 60 days ago: only in the 90-day window

        var item = await SingleItemAsync();

        Assert.Equal(1.0m, item.AveragePerDay30d);  // 10 units / 10 days of history in the 30-day window
        Assert.Equal(1.2m, item.AveragePerDay90d);  // 70 units / 60 days since the first sale
    }

    [Fact]
    public async Task GetItemsAsync_NothingSold_NoAverage()
    {
        Stock("A", available: 10);

        var item = await SingleItemAsync();

        Assert.Equal((null, null), (item.AveragePerDay30d, item.AveragePerDay90d));
    }

    [Fact]
    public async Task GetWorksheetAsync_GroupsAFamilyByColorInPaletteOrderWithSubtotals()
    {
        Stock("HTS00PU", available: 28, p => { p.FamilyId = 1; p.Color = ProductColor.Purple; p.HomeStock = 30; });
        Stock("FOB00BL", available: 24, p => { p.FamilyId = 1; p.Color = ProductColor.Blue; p.HomeStock = 75; });
        Stock("FOB00PU", available: 7, p => { p.FamilyId = 1; p.Color = ProductColor.Purple; p.HomeStock = 40; });
        Stock("SBP001", available: 5, p => p.FamilyId = 1);
        Stock("MAT-BLK", available: 90, p => { p.FamilyId = 2; p.Color = ProductColor.Black; });
        Sold("FOB00PU", Eastern(9, 10, 12), 3);
        Sold("HTS00PU", Eastern(9, 20, 12), 15);

        var sheet = await CreateService().GetWorksheetAsync(Us, 1, CancellationToken.None);

        Assert.Equal([ProductColor.Blue, ProductColor.Purple, null], sheet.Groups.Select(g => g.Color));
        Assert.Equal(["FOB00PU", "HTS00PU"], sheet.Groups[1].Items.Select(i => i.Sku));
        Assert.Equal((35, 70, 18), (sheet.Groups[1].Subtotals.Available, sheet.Groups[1].Subtotals.HomeStock, sheet.Groups[1].Subtotals.Sold30d));
        Assert.Equal((64, 145), (sheet.Totals.Available, sheet.Totals.HomeStock));
    }

    [Fact]
    public async Task GetWorksheetAsync_FamilyWithNoSkus_IsEmpty()
    {
        Stock("A", available: 1, p => p.FamilyId = 2);

        Assert.True((await CreateService().GetWorksheetAsync(Us, 1, CancellationToken.None)).IsEmpty);
    }
}
