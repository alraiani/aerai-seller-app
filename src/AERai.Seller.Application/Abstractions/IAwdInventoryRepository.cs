using AERai.Seller.Domain.Staging;

namespace AERai.Seller.Application.Abstractions;

public interface IAwdInventoryRepository
{
    /// <summary>Idempotent upsert keyed by (Sku, SnapshotDate) — safe to re-run for overlapping data.</summary>
    Task UpsertSnapshotsAsync(IEnumerable<AwdInventorySnapshot> snapshots, CancellationToken cancellationToken = default);

    /// <summary>The most recent snapshot per Sku, i.e. current AWD on-hand quantities.</summary>
    Task<IReadOnlyList<AwdInventorySnapshot>> GetLatestSnapshotsAsync(CancellationToken cancellationToken = default);
}
