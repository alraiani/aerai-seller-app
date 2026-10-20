using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IDeferredTransactionRepository"/> that records every pull applied.</summary>
internal sealed class FakeDeferredTransactionRepository : IDeferredTransactionRepository
{
    public List<(string MarketplaceId, List<AmazonDeferredTransaction> Held, List<AmazonDeferredTransaction> Released)> Applied { get; } = [];

    public int NewlyReleased { get; set; }

    public bool HasReleaseHistory { get; set; }

    public Task<DeferredApplyResult> ApplyAsync(
        string marketplaceId, IReadOnlyCollection<AmazonDeferredTransaction> stillDeferred, IReadOnlyCollection<AmazonDeferredTransaction> released, DateTimeOffset seenAt, CancellationToken cancellationToken)
    {
        Applied.Add((marketplaceId, [.. stillDeferred], [.. released]));
        return Task.FromResult(new DeferredApplyResult(stillDeferred.Count, released.Count, NewlyReleased));
    }

    public Task<bool> HasReleaseHistoryAsync(string marketplaceId, CancellationToken cancellationToken) => Task.FromResult(HasReleaseHistory);
}
