using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IAwdInventoryRepository"/> that records every snapshot replaced.</summary>
internal sealed class FakeAwdInventoryRepository : IAwdInventoryRepository
{
    public List<(string MarketplaceId, DateOnly SnapshotDate, List<AwdInventoryItem> Items)> Snapshots { get; } = [];

    public Task<int> ReplaceSnapshotAsync(string marketplaceId, DateOnly snapshotDate, IReadOnlyCollection<AwdInventoryItem> items, DateTimeOffset syncedAt, CancellationToken cancellationToken)
    {
        Snapshots.Add((marketplaceId, snapshotDate, [.. items]));
        return Task.FromResult(items.Count);
    }
}
