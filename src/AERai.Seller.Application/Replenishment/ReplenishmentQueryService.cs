using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Staging;

namespace AERai.Seller.Application.Replenishment;

/// <summary>Reader half of the Replenishment page's data — pairs with <see cref="IReplenishmentPlanningService"/>, the writer.</summary>
public sealed class ReplenishmentQueryService(
    IDemandForecastRepository demandForecastRepository,
    IReplenishmentRecommendationRepository replenishmentRecommendationRepository,
    IInventoryRepository inventoryRepository,
    IAwdInventoryRepository awdInventoryRepository,
    IProductRepository productRepository,
    ICatalogRepository catalogRepository,
    ILeadTimeProfileRepository leadTimeProfileRepository,
    IOrderRepository orderRepository,
    TimeProvider timeProvider) : IReplenishmentQueryService
{
    private const int OrderWindowDays = 90;

    public async Task<IReadOnlyList<ReplenishmentRow>> GetReplenishmentRowsAsync(CancellationToken cancellationToken = default)
    {
        var forecasts = await demandForecastRepository.GetLatestPerSkuAsync(cancellationToken);
        var recommendations = await replenishmentRecommendationRepository.GetLatestPerSkuAsync(cancellationToken);
        var fbaSnapshots = await inventoryRepository.GetLatestSnapshotsAsync(cancellationToken);
        var awdSnapshots = await awdInventoryRepository.GetLatestSnapshotsAsync(cancellationToken);
        var products = await productRepository.GetAllAsync(cancellationToken);
        var catalogItems = await catalogRepository.GetAllItemsAsync(cancellationToken);
        var leadTimeProfiles = await leadTimeProfileRepository.GetAllAsync(cancellationToken);

        var recommendationBySku = recommendations.ToDictionary(r => r.Sku);
        var fbaAvailableBySku = fbaSnapshots
            .Where(s => s.State == InventoryState.Available)
            .ToDictionary(s => s.Sku, s => s.Quantity);
        var awdOnhandBySku = awdSnapshots.ToDictionary(s => s.Sku, s => s.TotalOnhandQuantity);
        var costOfGoodsBySku = products
            .Where(p => p.CostOfGoods.HasValue)
            .ToDictionary(p => p.Sku, p => p.CostOfGoods!.Value);
        var titleBySku = catalogItems
            .Where(i => !string.IsNullOrEmpty(i.Sku))
            .GroupBy(i => i.Sku!)
            .ToDictionary(g => g.Key, g => g.First().Title);
        var hasLeadTimeProfileSkus = leadTimeProfiles.Select(p => p.Sku).ToHashSet();

        var rows = new List<ReplenishmentRow>(forecasts.Count);
        foreach (var forecast in forecasts)
        {
            if (!recommendationBySku.TryGetValue(forecast.Sku, out var recommendation)) continue;

            var hasInsufficientData = recommendation.DaysUntilActionNeeded == int.MaxValue;
            var costOfGoods = costOfGoodsBySku.GetValueOrDefault(forecast.Sku);

            rows.Add(new ReplenishmentRow(
                Sku: forecast.Sku,
                Title: titleBySku.GetValueOrDefault(forecast.Sku),
                FbaAvailable: fbaAvailableBySku.GetValueOrDefault(forecast.Sku),
                AwdOnhand: awdOnhandBySku.GetValueOrDefault(forecast.Sku),
                DaysOfSupply: forecast.DaysOfSupply,
                RecommendedOrderQuantity: recommendation.RecommendedOrderQuantity,
                RecommendedOrderBy: hasInsufficientData ? null : recommendation.RecommendedOrderBy,
                DaysUntilActionNeeded: recommendation.DaysUntilActionNeeded,
                HasInsufficientData: hasInsufficientData,
                HasLeadTimeProfile: hasLeadTimeProfileSkus.Contains(forecast.Sku),
                EstimatedReorderCost: costOfGoods == 0m ? null : costOfGoods * recommendation.RecommendedOrderQuantity));
        }

        return rows.OrderBy(r => r.DaysUntilActionNeeded).ToList();
    }

    public async Task<OrderCoverageStatus> GetOrderCoverageAsync(CancellationToken cancellationToken = default)
    {
        var earliestOrderDate = await orderRepository.GetEarliestOrderDateAsync(cancellationToken);
        var today = AmazonBusinessDay.TodayIn(timeProvider.GetUtcNow());
        var coverageThreshold = today.AddDays(-(OrderWindowDays - 1));

        var isSufficient = earliestOrderDate.HasValue && earliestOrderDate.Value <= coverageThreshold;
        return new OrderCoverageStatus(earliestOrderDate, isSufficient);
    }

    public async Task<IReadOnlyList<SalesVelocityPoint>> GetSalesVelocityTrendAsync(
        string sku, int days, CancellationToken cancellationToken = default)
    {
        var today = AmazonBusinessDay.TodayIn(timeProvider.GetUtcNow());
        var startDate = today.AddDays(-(days - 1));
        var orders = await orderRepository.GetOrdersPurchasedBetweenAsync(startDate, today, cancellationToken);

        var unitsByDay = orders
            .SelectMany(o => o.Items.Select(i => (Item: i, PurchaseDate: AmazonBusinessDay.DateOf(o.PurchaseDate))))
            .Where(x => x.Item.Sku == sku)
            .GroupBy(x => x.PurchaseDate)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Item.QuantityOrdered));

        return Enumerable.Range(0, days)
            .Select(offset => startDate.AddDays(offset))
            .Select(date => new SalesVelocityPoint(date, unitsByDay.GetValueOrDefault(date)))
            .ToList();
    }
}
