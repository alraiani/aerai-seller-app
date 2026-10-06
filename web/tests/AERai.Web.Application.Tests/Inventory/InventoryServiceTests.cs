using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Reporting;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class InventoryServiceTests
{
    // Mon 2026-10-05 15:00 UTC = 11:00 EDT (UTC-4).
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 15, 0, 0, TimeSpan.Zero);

    private static readonly Marketplace Us = TestMarketplaces.UnitedStates;

    private readonly FakeInventoryQueries _queries = new();

    private InventoryService CreateService() => new(_queries, new FakeTimeProvider(Now));

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
    public async Task GetItemsAsync_MostUrgentFirstAndUnknownVelocityLast()
    {
        Stock("SLOW", available: 300);
        Sold("SLOW", Eastern(9, 6, 12), 30);
        Stock("NEW", available: 1);
        Stock("URGENT", available: 3);
        Sold("URGENT", Eastern(9, 6, 12), 30);

        var items = await CreateService().GetItemsAsync(Us, CancellationToken.None);

        Assert.Equal(["URGENT", "SLOW", "NEW"], items.Select(i => i.Sku));
    }

    [Fact]
    public async Task GetOverviewAsync_SearchMatchesAsinAndTotalsCoverAllMatches()
    {
        Stock("MAT-BLK", available: 10, p => { p.Asin = "B0MAT1"; p.InboundWorking = 4; p.ReservedUnsplit = 2; });
        Stock("MAT-BLU", available: 20, p => p.Asin = "B0MAT2");
        Stock("STRAP", available: 99, p => p.Asin = "B0STR1");

        var overview = await CreateService().GetOverviewAsync(Us, new PageRequest(1, 1, "b0mat"), null, InventorySort.Urgency, CancellationToken.None);

        Assert.Equal(2, overview.Items.TotalCount);
        Assert.Single(overview.Items.Items);
        Assert.Equal((30, 4, 2, 32, 36), (overview.Totals.Available, overview.Totals.Inbound, overview.Totals.Reserved, overview.Totals.InWarehouse, overview.Totals.AmazonTotal));
    }
}
