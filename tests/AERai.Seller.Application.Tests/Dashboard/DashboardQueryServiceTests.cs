using AERai.Seller.Application.Abstractions;
using AERai.Seller.Application.Dashboard;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Staging;

namespace AERai.Seller.Application.Tests.Dashboard;

public class DashboardQueryServiceTests
{
    private sealed class FakeOrderRepository(IReadOnlyList<Order> orders) : IOrderRepository
    {
        public Task<int> InsertNewOrdersAsync(IEnumerable<Order> orders, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Order>> GetOrdersPurchasedOnAsync(DateOnly date, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Order>> GetOrdersPurchasedBetweenAsync(DateOnly startDateInclusive, DateOnly endDateInclusive, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Order>>(orders
                .Where(o =>
                {
                    var day = AmazonBusinessDay.DateOf(o.PurchaseDate);
                    return day >= startDateInclusive && day <= endDateInclusive;
                })
                .ToList());

        public Task<IReadOnlyList<OrderItem>> GetAllOrderItemsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DateOnly?> GetEarliestOrderDateAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingSyncMetadataRepository : ISyncMetadataRepository
    {
        public Task<SyncMetadata?> GetAsync(string syncJobName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<SyncMetadata>> GetAllAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RecordResultAsync(string syncJobName, bool succeeded, string? errorMessage, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static Order MakeOrder(DateOnly purchaseDay, params (string Sku, int Qty, decimal Price)[] items)
    {
        var purchaseDate = AmazonBusinessDay.StartOfDayUtc(purchaseDay).AddHours(12);
        return new Order
        {
            AmazonOrderId = Guid.NewGuid().ToString(),
            MarketplaceId = "ATVPDKIKX0DER",
            PurchaseDate = purchaseDate,
            OrderStatus = "Shipped",
            Items = items.Select(i => new OrderItem
            {
                AmazonOrderId = "unused",
                Sku = i.Sku,
                QuantityOrdered = i.Qty,
                ItemPrice = i.Price,
                ItemPriceCurrency = "USD",
            }).ToList(),
        };
    }

    [Fact]
    public async Task GetDailySalesForLastNDaysAsync_ReturnsSevenDaysAscending_WithGapsZeroFilled()
    {
        var endDate = new DateOnly(2026, 8, 2);
        var orders = new List<Order>
        {
            MakeOrder(endDate, ("SKU-A", 3, 10m)),
            MakeOrder(endDate.AddDays(-2), ("SKU-B", 2, 5m), ("SKU-C", 1, 20m)),
            MakeOrder(endDate.AddDays(-6), ("SKU-A", 1, 10m)),
            MakeOrder(endDate.AddDays(-7), ("SKU-A", 100, 999m)), // just outside the 7-day window
        };

        var service = new DashboardQueryService(new FakeOrderRepository(orders), new ThrowingSyncMetadataRepository());

        var result = await service.GetDailySalesForLastNDaysAsync(endDate, 7);

        Assert.Equal(7, result.Count);
        Assert.Equal(endDate.AddDays(-6), result[0].Date);
        Assert.Equal(endDate, result[6].Date);
        for (var i = 1; i < result.Count; i++)
        {
            Assert.Equal(result[i - 1].Date.AddDays(1), result[i].Date);
        }

        var windowStartDay = result.Single(d => d.Date == endDate.AddDays(-6));
        Assert.Equal(1, windowStartDay.Units);
        Assert.Equal(10m, windowStartDay.Revenue);

        var midWindowDay = result.Single(d => d.Date == endDate.AddDays(-2));
        Assert.Equal(3, midWindowDay.Units);
        Assert.Equal(30m, midWindowDay.Revenue);

        var lastDay = result.Single(d => d.Date == endDate);
        Assert.Equal(3, lastDay.Units);
        Assert.Equal(30m, lastDay.Revenue);

        // Gap days with no orders are zero-filled, not skipped.
        var gapDay = result.Single(d => d.Date == endDate.AddDays(-5));
        Assert.Equal(0, gapDay.Units);
        Assert.Equal(0m, gapDay.Revenue);

        // The day just outside the window must not leak into any bucket.
        Assert.DoesNotContain(result, d => d.Date == endDate.AddDays(-7));
        Assert.All(result, d => Assert.True(d.Units < 100));
    }
}
