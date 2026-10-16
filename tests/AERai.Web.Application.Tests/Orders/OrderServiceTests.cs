using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Orders;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Reporting;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Orders;

public sealed class OrderServiceTests
{
    private static readonly PageRequest Page = new();

    private static OrderSummary Order(string id, string marketplaceId, string currency, string status = "Shipped") => new()
    {
        AmazonOrderId = id, MarketplaceId = marketplaceId, OrderStatus = status, Currency = currency,
        PurchaseDate = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero), LineCount = 1, Units = 1, OrderTotal = 40m,
    };

    [Fact]
    public async Task GetListAsync_CanadianOrder_LooksUpPayoutsInTheUsListAndKeepsOnlyCad()
    {
        var queries = new FakeOrderQueries
        {
            Orders = [Order("CA-1", MarketplaceIds.Canada, "CAD")],
            Items = [new OrderItemRow("CA-1", "SKU-1", null, null, null, null, 1, 40m, 10m)],
            Amounts =
            [
                new OrderSettlementAmounts("CA-1", "S-CAD", "CAD", 40m, -8m, 0m, 0m),
                // Same order id in a payout of another currency (never expected, but it must not be added in).
                new OrderSettlementAmounts("CA-1", "S-USD", "USD", 99m, 0m, 0m, 0m),
            ],
        };
        var canada = TestMarketplaces.Canada;
        canada.SettlementMarketplaceId = MarketplaceIds.UnitedStates;

        var list = await new OrderService(queries, new FakeTimeProvider()).GetListAsync(canada, Page, OrderListFilter.Default, CancellationToken.None);

        Assert.Equal(MarketplaceIds.UnitedStates, queries.SettlementMarketplaceAsked);
        var row = Assert.Single(list.Orders.Items);
        Assert.Equal(["S-CAD"], row.Payout?.SettlementIds);
        Assert.Equal(32m, row.Payout?.Net);
        Assert.Equal(22m, row.Profit.Profit);
    }

    [Fact]
    public async Task GetListAsync_OrderWithoutSettlement_IsNotPaidOut()
    {
        var queries = new FakeOrderQueries
        {
            Orders = [Order("US-1", MarketplaceIds.UnitedStates, "USD")],
            Items = [new OrderItemRow("US-1", "SKU-1", null, null, null, null, 1, 40m, 10m)],
        };

        var list = await new OrderService(queries, new FakeTimeProvider()).GetListAsync(TestMarketplaces.UnitedStates, Page, OrderListFilter.Default, CancellationToken.None);

        Assert.Equal(MarketplaceIds.UnitedStates, queries.SettlementMarketplaceAsked);
        var row = Assert.Single(list.Orders.Items);
        Assert.Null(row.Payout);
        Assert.Equal(OrderProfitState.NotPaidOut, row.Profit.State);
        Assert.Single(row.Items);
    }

    [Fact]
    public async Task GetListAsync_EmptyPage_SkipsTheItemAndSettlementQueries()
    {
        var queries = new FakeOrderQueries();

        var list = await new OrderService(queries, new FakeTimeProvider()).GetListAsync(TestMarketplaces.UnitedStates, Page, OrderListFilter.Default, CancellationToken.None);

        Assert.Empty(list.Orders.Items);
        Assert.Null(queries.SettlementMarketplaceAsked);
    }

    [Fact]
    public async Task ExportAsync_MoreRowsThanTheCap_IsCutShortAndSaysSoInItsName()
    {
        var row = new OrderExportRow("US-1", DateTimeOffset.UnixEpoch, "Shipped", "USD", "SKU-1", null, null, 1, 10m, null);
        var queries = new FakeOrderQueries { ExportRows = Enumerable.Repeat(row, OrderService.MaxExportRows + 1).ToList() };
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));

        var export = await new OrderService(queries, time).ExportAsync(TestMarketplaces.UnitedStates, Page, OrderListFilter.Default, CancellationToken.None);

        Assert.True(export.Truncated);
        Assert.Equal($"orders-US-2026-10-16-first-{OrderService.MaxExportRows}-rows.csv", export.FileName);
        Assert.Equal(OrderService.MaxExportRows + 1, export.Content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task ExportAsync_WithinTheCap_IsNamedForTheMarketplaceAndLocalDay()
    {
        var queries = new FakeOrderQueries();
        // 02:00 UTC on the 17th is still the 16th in New York.
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 17, 2, 0, 0, TimeSpan.Zero));

        var export = await new OrderService(queries, time).ExportAsync(TestMarketplaces.UnitedStates, Page, OrderListFilter.Default, CancellationToken.None);

        Assert.False(export.Truncated);
        Assert.Equal("orders-US-2026-10-16.csv", export.FileName);
    }

    [Theory]
    [InlineData(OrderSort.Purchased)]
    [InlineData(OrderSort.Units)]
    [InlineData(OrderSort.Sales)]
    public void IsDescendingByDefault_EverySort_RunsHighToLow(OrderSort sort) => Assert.True(sort.IsDescendingByDefault());

    [Fact]
    public void AverageOrderValue_IgnoresCancelledOrders() =>
        Assert.Equal(50m, new OrderListTotals(Orders: 3, Cancelled: 1, Units: 4, Sales: 100m).AverageOrderValue);

    [Fact]
    public void AverageOrderValue_OnlyCancelled_IsNull() =>
        Assert.Null(new OrderListTotals(Orders: 2, Cancelled: 2, Units: 0, Sales: 0m).AverageOrderValue);

    private sealed class FakeOrderQueries : IOrderQueries
    {
        public List<OrderSummary> Orders { get; init; } = [];
        public List<OrderItemRow> Items { get; init; } = [];
        public List<OrderSettlementAmounts> Amounts { get; init; } = [];
        public List<OrderExportRow> ExportRows { get; init; } = [];
        public string? SettlementMarketplaceAsked { get; private set; }

        public Task<PagedResult<OrderSummary>> GetOrdersAsync(string marketplaceId, PageRequest request, OrderListFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<OrderSummary>(Orders, Orders.Count, 1, request.SafePageSize));

        public Task<OrderListTotals> GetTotalsAsync(string marketplaceId, PageRequest request, OrderListFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult(OrderListTotals.Empty);

        public Task<IReadOnlyList<string>> GetStatusesAsync(string marketplaceId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(["Shipped"]);

        public Task<IReadOnlyList<OrderItemRow>> GetItemsAsync(string marketplaceId, IReadOnlyCollection<string> amazonOrderIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OrderItemRow>>(Items.Where(i => amazonOrderIds.Contains(i.AmazonOrderId)).ToList());

        public Task<IReadOnlyList<OrderSettlementAmounts>> GetSettlementAmountsAsync(string settlementMarketplaceId, IReadOnlyCollection<string> amazonOrderIds, CancellationToken cancellationToken)
        {
            SettlementMarketplaceAsked = settlementMarketplaceId;
            return Task.FromResult<IReadOnlyList<OrderSettlementAmounts>>(Amounts.Where(a => amazonOrderIds.Contains(a.AmazonOrderId)).ToList());
        }

        public Task<IReadOnlyList<OrderExportRow>> GetExportRowsAsync(string marketplaceId, PageRequest request, OrderListFilter filter, int maxRows, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OrderExportRow>>(ExportRows.Take(maxRows).ToList());
    }
}
