using AERai.Seller.Application.Abstractions;
using AERai.Seller.Application.Replenishment;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Ai;
using AERai.Seller.Domain.Staging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AERai.Seller.Application.Tests.Replenishment;

public class ReplenishmentPlanningServiceTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
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

    private sealed class FakeOrderRepository(IReadOnlyList<Order> orders) : IOrderRepository
    {
        public Task<int> InsertNewOrdersAsync(IEnumerable<Order> o, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Order>> GetOrdersPurchasedOnAsync(DateOnly date, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Order>> GetOrdersPurchasedBetweenAsync(DateOnly start, DateOnly end, CancellationToken ct = default) => Task.FromResult(orders);
        public Task<IReadOnlyList<OrderItem>> GetAllOrderItemsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DateOnly?> GetEarliestOrderDateAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeLeadTimeProfileRepository(IReadOnlyList<LeadTimeProfile> profiles) : ILeadTimeProfileRepository
    {
        public Task UpsertAsync(LeadTimeProfile p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<LeadTimeProfile?> GetAsync(string sku, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeadTimeProfile>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(profiles);
    }

    private sealed class FakeAppSettingsStore(int defaultTargetStockDays) : IAppSettingsStore
    {
        public Task<AppSettings> GetAsync(CancellationToken ct = default)
            => Task.FromResult(AppSettings.CreateDefault() with { DefaultTargetStockDays = defaultTargetStockDays });
        public Task SaveAsync(AppSettings settings, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeDemandForecastRepository : IDemandForecastRepository
    {
        public List<DemandForecast> Inserted { get; } = [];
        public Task InsertAsync(IEnumerable<DemandForecast> forecasts, CancellationToken ct = default)
        {
            Inserted.AddRange(forecasts);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<DemandForecast>> GetLatestPerSkuAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeReplenishmentRecommendationRepository : IReplenishmentRecommendationRepository
    {
        public List<ReplenishmentRecommendation> Inserted { get; } = [];
        public Task InsertAsync(IEnumerable<ReplenishmentRecommendation> recs, CancellationToken ct = default)
        {
            Inserted.AddRange(recs);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<ReplenishmentRecommendation>> GetLatestPerSkuAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeSyncMetadataRepository : ISyncMetadataRepository
    {
        public bool? LastSucceeded { get; private set; }
        public string? LastJobName { get; private set; }
        public Task<SyncMetadata?> GetAsync(string syncJobName, CancellationToken ct = default) => Task.FromResult<SyncMetadata?>(null);
        public Task<IReadOnlyList<SyncMetadata>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SyncMetadata>>([]);
        public Task RecordResultAsync(string syncJobName, bool succeeded, string? errorMessage, CancellationToken ct = default)
        {
            LastJobName = syncJobName;
            LastSucceeded = succeeded;
            return Task.CompletedTask;
        }
    }

    private static readonly DateOnly Today = new(2026, 8, 3);
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>90 orders of 1 unit each, one per day for the last 90 days — a clean 1.0 unit/day velocity.</summary>
    private static List<Order> NinetyDaysOfOneUnitPerDayOrders(string sku)
        => Enumerable.Range(0, 90)
            .Select(offset => new Order
            {
                AmazonOrderId = $"ORDER-{offset}",
                MarketplaceId = "ATVPDKIKX0DER",
                PurchaseDate = AmazonBusinessDay.StartOfDayUtc(Today.AddDays(-offset)).AddHours(12),
                OrderStatus = "Shipped",
                Items = [new OrderItem { AmazonOrderId = $"ORDER-{offset}", Sku = sku, QuantityOrdered = 1 }],
            })
            .ToList();

    private static ReplenishmentPlanningService CreateService(
        IReadOnlyList<InventorySnapshot> fbaSnapshots,
        IReadOnlyList<AwdInventorySnapshot> awdSnapshots,
        IReadOnlyList<Order> orders,
        IReadOnlyList<LeadTimeProfile> leadTimeProfiles,
        int defaultTargetStockDays,
        out FakeDemandForecastRepository demandForecastRepository,
        out FakeReplenishmentRecommendationRepository recommendationRepository,
        out FakeSyncMetadataRepository syncMetadataRepository)
    {
        demandForecastRepository = new FakeDemandForecastRepository();
        recommendationRepository = new FakeReplenishmentRecommendationRepository();
        syncMetadataRepository = new FakeSyncMetadataRepository();

        return new ReplenishmentPlanningService(
            new FakeInventoryRepository(fbaSnapshots),
            new FakeAwdInventoryRepository(awdSnapshots),
            new FakeOrderRepository(orders),
            new FakeLeadTimeProfileRepository(leadTimeProfiles),
            new FakeAppSettingsStore(defaultTargetStockDays),
            demandForecastRepository,
            recommendationRepository,
            syncMetadataRepository,
            new FixedTimeProvider(Now),
            NullLogger<ReplenishmentPlanningService>.Instance);
    }

    [Fact]
    public async Task RecalculateAsync_PerSkuTargetStockDaysOverride_TakesPrecedenceOverGlobalDefault()
    {
        var fba = new List<InventorySnapshot> { new() { Sku = "SKU-A", State = InventoryState.Available, Quantity = 20, SnapshotDate = Today } };
        var awd = new List<AwdInventorySnapshot>
        {
            new() { Sku = "SKU-A", SnapshotDate = Today, AvailableDistributableQuantity = 5, ReplenishmentQuantity = 5, TotalInboundQuantity = 999 },
        };
        var profiles = new List<LeadTimeProfile>
        {
            new() { Sku = "SKU-A", SupplierLeadTimeDays = 10, PrepTimeDays = 5, FbaTransitDays = 3, SafetyStockDays = 2, TargetStockDays = 60 },
        };

        var service = CreateService(
            fba, awd, NinetyDaysOfOneUnitPerDayOrders("SKU-A"), profiles, defaultTargetStockDays: 45,
            out var forecasts, out var recommendations, out _);

        await service.RecalculateAsync();

        var forecast = Assert.Single(forecasts.Inserted);
        Assert.Equal(1.0m, forecast.DailySalesVelocity);
        // SellThroughEligibleStock = FBA Available (20) + AWD (Available 5 + Replenishment 5) = 30;
        // AWD's TotalInboundQuantity (999, not yet received) must NOT be counted.
        Assert.Equal(30, forecast.SellThroughEligibleStock);
        Assert.Equal(30m, forecast.DaysOfSupply);
        Assert.Equal(Today.AddDays(30), forecast.ProjectedStockoutDate);

        var recommendation = Assert.Single(recommendations.Inserted);
        // RecommendedOrderQuantity = round(velocity * targetStockDays) - stock = round(1.0*60) - 30 = 30.
        Assert.Equal(30, recommendation.RecommendedOrderQuantity);
        // RecommendedOrderBy = stockout(+30) - (safety 2 + fbaTransit 3 + prep 5 + supplier 10) = +10.
        Assert.Equal(Today.AddDays(10), recommendation.RecommendedOrderBy);
        Assert.Equal(10, recommendation.DaysUntilActionNeeded);
        // PrepDueBy/ShipToFbaBy = RecommendedOrderBy + supplier(10) + prep(5) = +25.
        Assert.Equal(Today.AddDays(25), recommendation.PrepDueBy);
        Assert.Equal(Today.AddDays(25), recommendation.ShipToFbaBy);
    }

    [Fact]
    public async Task RecalculateAsync_NoPerSkuOverride_FallsBackToGlobalDefaultTargetStockDays()
    {
        var fba = new List<InventorySnapshot> { new() { Sku = "SKU-A", State = InventoryState.Available, Quantity = 20, SnapshotDate = Today } };
        var awd = new List<AwdInventorySnapshot>
        {
            new() { Sku = "SKU-A", SnapshotDate = Today, AvailableDistributableQuantity = 5, ReplenishmentQuantity = 5 },
        };
        var profiles = new List<LeadTimeProfile>
        {
            new() { Sku = "SKU-A", SupplierLeadTimeDays = 10, PrepTimeDays = 5, FbaTransitDays = 3, SafetyStockDays = 2, TargetStockDays = null },
        };

        var service = CreateService(
            fba, awd, NinetyDaysOfOneUnitPerDayOrders("SKU-A"), profiles, defaultTargetStockDays: 45,
            out _, out var recommendations, out _);

        await service.RecalculateAsync();

        var recommendation = Assert.Single(recommendations.Inserted);
        // round(1.0 * 45) - 30 = 15.
        Assert.Equal(15, recommendation.RecommendedOrderQuantity);
    }

    [Fact]
    public async Task RecalculateAsync_CoverageBelowMinimumDays_TreatsAsInsufficientData()
    {
        var fba = new List<InventorySnapshot> { new() { Sku = "SKU-A", State = InventoryState.Available, Quantity = 20, SnapshotDate = Today } };
        // 10 units sold, but only over the last 5 days — below the 14-day coverage minimum.
        var orders = Enumerable.Range(0, 5)
            .Select(offset => new Order
            {
                AmazonOrderId = $"ORDER-{offset}",
                MarketplaceId = "ATVPDKIKX0DER",
                PurchaseDate = AmazonBusinessDay.StartOfDayUtc(Today.AddDays(-offset)).AddHours(12),
                OrderStatus = "Shipped",
                Items = [new OrderItem { AmazonOrderId = $"ORDER-{offset}", Sku = "SKU-A", QuantityOrdered = 2 }],
            })
            .ToList();

        var service = CreateService(
            fba, [], orders, [], defaultTargetStockDays: 45, out var forecasts, out var recommendations, out _);

        await service.RecalculateAsync();

        var forecast = Assert.Single(forecasts.Inserted);
        Assert.Equal(0m, forecast.DailySalesVelocity);
        Assert.Equal(0m, forecast.DaysOfSupply);
        Assert.Null(forecast.ProjectedStockoutDate);

        var recommendation = Assert.Single(recommendations.Inserted);
        Assert.Equal(0, recommendation.RecommendedOrderQuantity);
        Assert.Equal(DateOnly.MaxValue, recommendation.RecommendedOrderBy);
        Assert.Equal(int.MaxValue, recommendation.DaysUntilActionNeeded);
    }

    [Fact]
    public async Task RecalculateAsync_TooFewUnitsSoldDespiteLongCoverage_TreatsAsInsufficientData()
    {
        var fba = new List<InventorySnapshot> { new() { Sku = "SKU-A", State = InventoryState.Available, Quantity = 20, SnapshotDate = Today } };
        // Full 90-day coverage, but only 3 total units sold — below the 5-unit minimum.
        var orders = new List<Order>
        {
            new()
            {
                AmazonOrderId = "ORDER-1",
                MarketplaceId = "ATVPDKIKX0DER",
                PurchaseDate = AmazonBusinessDay.StartOfDayUtc(Today.AddDays(-89)).AddHours(12),
                OrderStatus = "Shipped",
                Items = [new OrderItem { AmazonOrderId = "ORDER-1", Sku = "SKU-A", QuantityOrdered = 3 }],
            },
        };

        var service = CreateService(
            fba, [], orders, [], defaultTargetStockDays: 45, out var forecasts, out _, out _);

        var forecast = Assert.Single((await RecalculateAndReturnForecasts(service, forecasts)));
        Assert.Equal(0m, forecast.DailySalesVelocity);
    }

    private static async Task<List<DemandForecast>> RecalculateAndReturnForecasts(
        IReplenishmentPlanningService service, FakeDemandForecastRepository repo)
    {
        await service.RecalculateAsync();
        return repo.Inserted;
    }

    [Fact]
    public async Task RecalculateAsync_SkuWithNoLeadTimeProfile_DefaultsToZeroLeadTimes_WithoutThrowing()
    {
        var fba = new List<InventorySnapshot> { new() { Sku = "SKU-A", State = InventoryState.Available, Quantity = 20, SnapshotDate = Today } };

        var service = CreateService(
            fba, [], NinetyDaysOfOneUnitPerDayOrders("SKU-A"), leadTimeProfiles: [], defaultTargetStockDays: 45,
            out var forecasts, out var recommendations, out _);

        await service.RecalculateAsync();

        var forecast = Assert.Single(forecasts.Inserted);
        Assert.Equal(Today.AddDays(20), forecast.ProjectedStockoutDate); // 20 units / 1.0 per day

        var recommendation = Assert.Single(recommendations.Inserted);
        // All lead times default to 0, so RecommendedOrderBy == the stockout date itself.
        Assert.Equal(forecast.ProjectedStockoutDate, recommendation.RecommendedOrderBy);
        Assert.Equal(recommendation.RecommendedOrderBy, recommendation.PrepDueBy);
        Assert.Equal(recommendation.RecommendedOrderBy, recommendation.ShipToFbaBy);
    }

    [Fact]
    public async Task RecalculateAsync_RecordsSyncMetadataUnderReplenishmentPlanningJobName()
    {
        var service = CreateService([], [], [], [], defaultTargetStockDays: 45, out _, out _, out var syncMetadataRepository);

        await service.RecalculateAsync();

        Assert.Equal(ReplenishmentPlanningService.JobName, syncMetadataRepository.LastJobName);
        Assert.True(syncMetadataRepository.LastSucceeded);
    }
}
