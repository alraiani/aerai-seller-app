namespace AERai.Seller.Application.Replenishment;

public sealed record ReplenishmentRow(
    string Sku,
    string? Title,
    int FbaAvailable,
    int AwdOnhand,
    decimal DaysOfSupply,
    int RecommendedOrderQuantity,
    DateOnly? RecommendedOrderBy,
    int DaysUntilActionNeeded,
    bool HasInsufficientData,
    bool HasLeadTimeProfile,
    decimal? EstimatedReorderCost);

public sealed record OrderCoverageStatus(DateOnly? EarliestOrderDate, bool IsSufficient);

public sealed record SalesVelocityPoint(DateOnly Date, int Units);

public interface IReplenishmentQueryService
{
    /// <summary>Latest forecast + recommendation per SKU, joined with product/catalog display data, sorted by urgency ascending.</summary>
    Task<IReadOnlyList<ReplenishmentRow>> GetReplenishmentRowsAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether local order history covers the last 90 days, for the "fetch more history" warning banner.</summary>
    Task<OrderCoverageStatus> GetOrderCoverageAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesVelocityPoint>> GetSalesVelocityTrendAsync(
        string sku, int days, CancellationToken cancellationToken = default);
}
