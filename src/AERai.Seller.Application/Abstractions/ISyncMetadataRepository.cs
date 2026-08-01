using AERai.Seller.Domain;

namespace AERai.Seller.Application.Abstractions;

public interface ISyncMetadataRepository
{
    Task<SyncMetadata?> GetAsync(string syncJobName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncMetadata>> GetAllAsync(CancellationToken cancellationToken = default);
    Task RecordResultAsync(string syncJobName, bool succeeded, string? errorMessage, CancellationToken cancellationToken = default);
}
