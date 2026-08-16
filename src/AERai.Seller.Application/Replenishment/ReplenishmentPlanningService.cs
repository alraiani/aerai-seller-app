using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Ai;
using AERai.Seller.Domain.Staging;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Application.Replenishment;

/// <summary>
/// Computes demand velocity and a reorder recommendation per SKU. Everything is loaded once up
/// front (latest FBA/AWD snapshots, all lead time profiles, settings, one 90-day order window)
/// and the per-SKU math runs in memory — no per-SKU queries.
/// </summary>
public sealed class ReplenishmentPlanningService(
    IInventoryRepository inventoryRepository,
    IAwdInventoryRepository awdInventoryRepository,
    IOrderRepository orderRepository,
    ILeadTimeProfileRepository leadTimeProfileRepository,
    IAppSettingsStore appSettingsStore,
    IDemandForecastRepository demandForecastRepository,
    IReplenishmentRecommendationRepository replenishmentRecommendationRepository,
    ISyncMetadataRepository syncMetadataRepository,
    TimeProvider timeProvider,
    ILogger<ReplenishmentPlanningService> logger) : IReplenishmentPlanningService
{
    public const string JobName = "ReplenishmentPlanning";

    // A velocity estimate from very little history or very few sales is more likely to mislead
    // than help — flag these SKUs as insufficient rather than compute a misleadingly precise
    // days-of-supply/order-by date from them.
    private const int MinCoverageDaysForConfidentVelocity = 14;
    private const int MinUnitsSoldForConfidentVelocity = 5;
    private const int OrderWindowDays = 90;

    private static readonly InventoryState[] FbaSellThroughStates =
    [
        InventoryState.Available, InventoryState.Inbound, InventoryState.FcTransfer, InventoryState.FcProcessing,
    ];

    public async Task<int> RecalculateAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            progress?.Report("Replenishment: loading current inventory, lead times, and order history...");

            var now = timeProvider.GetUtcNow();
            var today = AmazonBusinessDay.TodayIn(now);
            var windowStart = today.AddDays(-(OrderWindowDays - 1));

            var fbaSnapshots = await inventoryRepository.GetLatestSnapshotsAsync(cancellationToken);
            var awdSnapshots = await awdInventoryRepository.GetLatestSnapshotsAsync(cancellationToken);
            var leadTimeProfiles = await leadTimeProfileRepository.GetAllAsync(cancellationToken);
            var appSettings = await appSettingsStore.GetAsync(cancellationToken);
            var orders = await orderRepository.GetOrdersPurchasedBetweenAsync(windowStart, today, cancellationToken);

            var fbaBySku = fbaSnapshots
                .Where(s => FbaSellThroughStates.Contains(s.State))
                .GroupBy(s => s.Sku)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));

            var awdBySku = awdSnapshots
                .ToDictionary(s => s.Sku, s => s.AvailableDistributableQuantity + s.ReplenishmentQuantity);

            var leadTimeBySku = leadTimeProfiles.ToDictionary(p => p.Sku);

            var orderItemsBySku = orders
                .SelectMany(o => o.Items.Select(i => (Item: i, PurchaseDate: AmazonBusinessDay.DateOf(o.PurchaseDate))))
                .GroupBy(x => x.Item.Sku)
                .ToDictionary(g => g.Key, g => g.ToList());

            var skuUniverse = fbaBySku.Keys
                .Union(awdBySku.Keys)
                .Union(leadTimeBySku.Keys)
                .Union(orderItemsBySku.Keys)
                .Distinct()
                .ToList();

            progress?.Report($"Replenishment: computing forecasts for {skuUniverse.Count} SKU(s)...");

            var forecasts = new List<DemandForecast>(skuUniverse.Count);
            var recommendations = new List<ReplenishmentRecommendation>(skuUniverse.Count);

            foreach (var sku in skuUniverse)
            {
                var sellThroughEligibleStock = fbaBySku.GetValueOrDefault(sku) + awdBySku.GetValueOrDefault(sku);
                var skuOrderData = orderItemsBySku.GetValueOrDefault(sku);
                var totalUnitsSold = skuOrderData?.Sum(x => x.Item.QuantityOrdered) ?? 0;
                var earliestOrderDate = skuOrderData is { Count: > 0 } ? skuOrderData.Min(x => x.PurchaseDate) : (DateOnly?)null;
                var coverageDays = earliestOrderDate.HasValue
                    ? Math.Min(OrderWindowDays, today.DayNumber - earliestOrderDate.Value.DayNumber + 1)
                    : 0;

                var isInsufficientData = coverageDays < MinCoverageDaysForConfidentVelocity
                    || totalUnitsSold < MinUnitsSoldForConfidentVelocity;

                decimal dailySalesVelocity = 0m;
                decimal daysOfSupply = 0m;
                DateOnly? projectedStockoutDate = null;

                if (!isInsufficientData)
                {
                    dailySalesVelocity = (decimal)totalUnitsSold / coverageDays;
                    daysOfSupply = sellThroughEligibleStock / dailySalesVelocity;
                    projectedStockoutDate = today.AddDays((int)Math.Floor(daysOfSupply));
                }

                forecasts.Add(new DemandForecast
                {
                    Sku = sku,
                    ComputedAt = now,
                    DailySalesVelocity = dailySalesVelocity,
                    SellThroughEligibleStock = sellThroughEligibleStock,
                    DaysOfSupply = daysOfSupply,
                    ProjectedStockoutDate = projectedStockoutDate,
                });

                var profile = leadTimeBySku.GetValueOrDefault(sku);
                var supplierLeadTimeDays = profile?.SupplierLeadTimeDays ?? 0;
                var prepTimeDays = profile?.PrepTimeDays ?? 0;
                var fbaTransitDays = profile?.FbaTransitDays ?? 0;
                var safetyStockDays = profile?.SafetyStockDays ?? 0;
                var effectiveTargetStockDays = profile?.TargetStockDays ?? appSettings.DefaultTargetStockDays;

                int recommendedOrderQuantity;
                DateOnly recommendedOrderBy;
                DateOnly prepDueBy;
                DateOnly shipToFbaBy;
                int daysUntilActionNeeded;

                if (isInsufficientData)
                {
                    // Velocity is 0, so there's nothing actionable to recommend yet — sentinel
                    // values sort these SKUs to the bottom of the urgency list rather than making
                    // them look either falsely urgent or falsely safe.
                    recommendedOrderQuantity = 0;
                    recommendedOrderBy = DateOnly.MaxValue;
                    prepDueBy = DateOnly.MaxValue;
                    shipToFbaBy = DateOnly.MaxValue;
                    daysUntilActionNeeded = int.MaxValue;
                }
                else
                {
                    recommendedOrderQuantity = Math.Max(
                        0, (int)Math.Round(dailySalesVelocity * effectiveTargetStockDays) - sellThroughEligibleStock);

                    // Work backward from the projected stockout date through the whole pipeline
                    // (supplier -> prep -> FBA transit), holding back the safety buffer, to find
                    // the last date an order can be placed and still land in time.
                    var totalPipelineDays = safetyStockDays + fbaTransitDays + prepTimeDays + supplierLeadTimeDays;
                    recommendedOrderBy = projectedStockoutDate!.Value.AddDays(-totalPipelineDays);
                    prepDueBy = recommendedOrderBy.AddDays(supplierLeadTimeDays + prepTimeDays);
                    shipToFbaBy = prepDueBy;
                    daysUntilActionNeeded = recommendedOrderBy.DayNumber - today.DayNumber;
                }

                recommendations.Add(new ReplenishmentRecommendation
                {
                    Sku = sku,
                    ComputedAt = now,
                    RecommendedOrderQuantity = recommendedOrderQuantity,
                    RecommendedOrderBy = recommendedOrderBy,
                    UnitsDueOutOfPrep = recommendedOrderQuantity,
                    PrepDueBy = prepDueBy,
                    UnitsToShipToFba = recommendedOrderQuantity,
                    ShipToFbaBy = shipToFbaBy,
                    DaysUntilActionNeeded = daysUntilActionNeeded,
                });
            }

            await demandForecastRepository.InsertAsync(forecasts, cancellationToken);
            await replenishmentRecommendationRepository.InsertAsync(recommendations, cancellationToken);
            await syncMetadataRepository.RecordResultAsync(JobName, succeeded: true, errorMessage: null, cancellationToken);

            progress?.Report($"Replenishment: done, {skuUniverse.Count} SKU(s) recalculated.");
            logger.LogInformation("Replenishment planning completed for {SkuCount} SKUs", skuUniverse.Count);

            return skuUniverse.Count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Replenishment planning failed");
            await syncMetadataRepository.RecordResultAsync(JobName, succeeded: false, ex.Message, cancellationToken);
            throw;
        }
    }
}
