using AERai.Seller.Application.Abstractions;
using AERai.Seller.Application.Replenishment;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Ai;
using AERai.Seller.Domain.Staging;
using Xunit;

namespace AERai.Seller.Application.Tests.Replenishment;

public class ReplenishmentQueryServiceTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeDemandForecastRepository(IReadOnlyList<DemandForecast> forecasts) : IDemandForecastRepository
    {
        public Task InsertAsync(IEnumerable<DemandForecast> f, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DemandForecast>> GetLatestPerSkuAsync(CancellationToken ct = default) => Task.FromResult(forecasts);
    }

    private sealed class FakeReplenishmentRecommendationRepository(IReadOnlyList<ReplenishmentRecommendation> recs)
        : IReplenishmentRecommendationRepository
    {
        public Task InsertAsync(IEnumerable<ReplenishmentRecommendation> r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReplenishmentRecommendation>> GetLatestPerSkuAsync(CancellationToken ct = default) => Task.FromResult(recs);
    }

    private sealed class FakeInventoryRepository(IReadOnlyList<InventorySnapshot> snapshots) : IInventoryRepository
    {
        public Task UpsertSnapshotsAsync(IEnumerable<InventorySnapshot> s, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<InventorySnapshot>> GetLatestSnapshotsAsync(CancellationToken ct = default) => Task.FromResult(snapshots);
    }

    private sealed class FakeAwdInventoryRepository(IReadOnlyList<AwdInventorySnapshot> snapshots) : IAwdInventoryRepository
    {
        public Task UpsertSnapshotsAsync(IEnumerable<AwdInventorySnapshot> s, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AwdInventorySnapshot>> GetLatestSnapshotsAsync(CancellationToken ct = default) => Task.FromResult(snapshots);
    }

    private sealed class FakeProductRepository(IReadOnlyList<Product> products) : IProductRepository
    {
        public Task UpsertAsync(Product p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(products);
    }

    private sealed class FakeCatalogRepository(IReadOnlyList<CatalogItem> items) : ICatalogRepository
    {
        public Task UpsertAsync(IEnumerable<CatalogItem> i, IEnumerable<CatalogParent> p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogItem>> GetAllItemsAsync(CancellationToken ct = default) => Task.FromResult(items);
        public Task<IReadOnlyList<CatalogParent>> GetAllParentsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CatalogParent>>([]);
    }

    private sealed class FakeLeadTimeProfileRepository(IReadOnlyList<LeadTimeProfile> profiles) : ILeadTimeProfileRepository
    {
        public Task UpsertAsync(LeadTimeProfile p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<LeadTimeProfile?> GetAsync(string sku, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeadTimeProfile>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(profiles);
    }

    private sealed class FakeOrderRepository(DateOnly? earliestOrderDate) : IOrderRepository
    {
        public Task<int> InsertNewOrdersAsync(IEnumerable<Order> o, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Order>> GetOrdersPurchasedOnAsync(DateOnly date, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Order>> GetOrdersPurchasedBetweenAsync(DateOnly start, DateOnly end, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Order>>([]);
        public Task<IReadOnlyList<OrderItem>> GetAllOrderItemsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DateOnly?> GetEarliestOrderDateAsync(CancellationToken ct = default) => Task.FromResult(earliestOrderDate);
    }

    private static readonly DateOnly Today = new(2026, 8, 3);
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    private static ReplenishmentQueryService CreateService(
        DateOnly? earliestOrderDate = null,
        IReadOnlyList<DemandForecast>? forecasts = null,
        IReadOnlyList<ReplenishmentRecommendation>? recommendations = null,
        IReadOnlyList<InventorySnapshot>? fbaSnapshots = null,
        IReadOnlyList<AwdInventorySnapshot>? awdSnapshots = null,
        IReadOnlyList<Product>? products = null,
        IReadOnlyList<CatalogItem>? catalogItems = null,
        IReadOnlyList<LeadTimeProfile>? leadTimeProfiles = null)
        => new(
            new FakeDemandForecastRepository(forecasts ?? []),
            new FakeReplenishmentRecommendationRepository(recommendations ?? []),
            new FakeInventoryRepository(fbaSnapshots ?? []),
            new FakeAwdInventoryRepository(awdSnapshots ?? []),
            new FakeProductRepository(products ?? []),
            new FakeCatalogRepository(catalogItems ?? []),
            new FakeLeadTimeProfileRepository(leadTimeProfiles ?? []),
            new FakeOrderRepository(earliestOrderDate),
            new FixedTimeProvider(Now));

    [Fact]
    public async Task GetOrderCoverageAsync_NoOrdersYet_IsInsufficient()
    {
        var service = CreateService(earliestOrderDate: null);

        var coverage = await service.GetOrderCoverageAsync();

        Assert.Null(coverage.EarliestOrderDate);
        Assert.False(coverage.IsSufficient);
    }

    [Fact]
    public async Task GetOrderCoverageAsync_EarliestOrderNewerThan90Days_IsInsufficient()
    {
        var service = CreateService(earliestOrderDate: Today.AddDays(-10));

        var coverage = await service.GetOrderCoverageAsync();

        Assert.False(coverage.IsSufficient);
    }

    [Fact]
    public async Task GetOrderCoverageAsync_EarliestOrderAtLeast90DaysAgo_IsSufficient()
    {
        var service = CreateService(earliestOrderDate: Today.AddDays(-89));

        var coverage = await service.GetOrderCoverageAsync();

        Assert.True(coverage.IsSufficient);
    }

    [Fact]
    public async Task GetReplenishmentRowsAsync_JoinsDisplayDataAndSortsByUrgencyAscending()
    {
        var forecasts = new List<DemandForecast>
        {
            new() { Sku = "SKU-URGENT", ComputedAt = Now, DaysOfSupply = 5m },
            new() { Sku = "SKU-CALM", ComputedAt = Now, DaysOfSupply = 60m },
        };
        var recommendations = new List<ReplenishmentRecommendation>
        {
            new() { Sku = "SKU-URGENT", ComputedAt = Now, RecommendedOrderQuantity = 50, RecommendedOrderBy = Today, PrepDueBy = Today, ShipToFbaBy = Today, DaysUntilActionNeeded = 0 },
            new() { Sku = "SKU-CALM", ComputedAt = Now, RecommendedOrderQuantity = 10, RecommendedOrderBy = Today.AddDays(40), PrepDueBy = Today.AddDays(40), ShipToFbaBy = Today.AddDays(40), DaysUntilActionNeeded = 40 },
        };
        var fbaSnapshots = new List<InventorySnapshot>
        {
            new() { Sku = "SKU-URGENT", State = InventoryState.Available, Quantity = 3, SnapshotDate = Today },
        };
        var awdSnapshots = new List<AwdInventorySnapshot>
        {
            new() { Sku = "SKU-URGENT", SnapshotDate = Today, TotalOnhandQuantity = 7 },
        };
        var products = new List<Product> { new() { Sku = "SKU-URGENT", CostOfGoods = 2.50m } };
        var catalogItems = new List<CatalogItem> { new() { Asin = "B0URGENT", Sku = "SKU-URGENT", Title = "Urgent Widget" } };
        var leadTimeProfiles = new List<LeadTimeProfile> { new() { Sku = "SKU-CALM", SupplierLeadTimeDays = 5 } };

        var service = CreateService(
            earliestOrderDate: Today.AddDays(-89),
            forecasts: forecasts, recommendations: recommendations,
            fbaSnapshots: fbaSnapshots, awdSnapshots: awdSnapshots,
            products: products, catalogItems: catalogItems, leadTimeProfiles: leadTimeProfiles);

        var rows = await service.GetReplenishmentRowsAsync();

        Assert.Equal(2, rows.Count);
        // Sorted ascending by urgency: SKU-URGENT (0 days) before SKU-CALM (40 days).
        Assert.Equal("SKU-URGENT", rows[0].Sku);
        Assert.Equal("Urgent Widget", rows[0].Title);
        Assert.Equal(3, rows[0].FbaAvailable);
        Assert.Equal(7, rows[0].AwdOnhand);
        Assert.Equal(125.00m, rows[0].EstimatedReorderCost); // 50 units * $2.50
        Assert.False(rows[0].HasInsufficientData);
        Assert.False(rows[0].HasLeadTimeProfile); // no LeadTimeProfile row for SKU-URGENT

        Assert.Equal("SKU-CALM", rows[1].Sku);
        Assert.True(rows[1].HasLeadTimeProfile);
        Assert.Null(rows[1].EstimatedReorderCost); // no Product/CostOfGoods known for SKU-CALM
    }

    [Fact]
    public async Task GetReplenishmentRowsAsync_SentinelDaysUntilActionNeeded_MarkedAsInsufficientData_WithNullOrderByDate()
    {
        var forecasts = new List<DemandForecast> { new() { Sku = "SKU-NEW", ComputedAt = Now, DaysOfSupply = 0m } };
        var recommendations = new List<ReplenishmentRecommendation>
        {
            new() { Sku = "SKU-NEW", ComputedAt = Now, RecommendedOrderQuantity = 0, RecommendedOrderBy = DateOnly.MaxValue, PrepDueBy = DateOnly.MaxValue, ShipToFbaBy = DateOnly.MaxValue, DaysUntilActionNeeded = int.MaxValue },
        };

        var service = CreateService(earliestOrderDate: Today, forecasts: forecasts, recommendations: recommendations);

        var row = Assert.Single(await service.GetReplenishmentRowsAsync());

        Assert.True(row.HasInsufficientData);
        Assert.Null(row.RecommendedOrderBy);
    }
}
