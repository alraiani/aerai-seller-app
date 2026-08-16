using AERai.Seller.Domain.Ai;

namespace AERai.Seller.Application.Abstractions;

public interface IReplenishmentRecommendationRepository
{
    /// <summary>Append-only — recommendations are historized, never updated in place.</summary>
    Task InsertAsync(IEnumerable<ReplenishmentRecommendation> recommendations, CancellationToken cancellationToken = default);

    /// <summary>The most recent recommendation per Sku (by ComputedAt).</summary>
    Task<IReadOnlyList<ReplenishmentRecommendation>> GetLatestPerSkuAsync(CancellationToken cancellationToken = default);
}
