using AERai.Seller.Domain.Ai;

namespace AERai.Seller.Application.Abstractions;

public interface ILeadTimeProfileRepository
{
    /// <summary>Idempotent upsert keyed by Sku.</summary>
    Task UpsertAsync(LeadTimeProfile profile, CancellationToken cancellationToken = default);

    Task<LeadTimeProfile?> GetAsync(string sku, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeadTimeProfile>> GetAllAsync(CancellationToken cancellationToken = default);
}
