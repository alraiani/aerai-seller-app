using AERai.Seller.Domain;

namespace AERai.Seller.Application.Abstractions;

public interface IInventoryRepository
{
    /// <summary>Idempotent upsert keyed by (Sku, State, SnapshotDate) — safe to re-run for overlapping data.</summary>
    Task UpsertSnapshotsAsync(IEnumerable<InventorySnapshot> snapshots, CancellationToken cancellationToken = default);

    /// <summary>The most recent snapshot per (Sku, State), i.e. current on-hand quantities.</summary>
    Task<IReadOnlyList<InventorySnapshot>> GetLatestSnapshotsAsync(CancellationToken cancellationToken = default);
}
