namespace AERai.Seller.Application.Replenishment;

public interface IReplenishmentPlanningService
{
    /// <summary>
    /// Recomputes demand forecasts and replenishment recommendations for every known SKU from
    /// current inventory (FBA + AWD), lead time profiles, and 90 days of order history, appending
    /// one new historized row per SKU to each of DemandForecast/ReplenishmentRecommendation.
    /// Always runs a fresh computation — staleness/"is it worth recomputing" checks belong to the
    /// caller (see SyncMetadata job "ReplenishmentPlanning" for last-computed-at).
    /// </summary>
    /// <returns>The number of SKUs processed.</returns>
    Task<int> RecalculateAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}
