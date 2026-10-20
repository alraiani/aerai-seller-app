using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IDeferredTransactionRepository"/> that records every listing applied.</summary>
internal sealed class FakeDeferredTransactionRepository : IDeferredTransactionRepository
{
    public List<(string MarketplaceId, List<DeferredTransactionItem> Items)> Applied { get; } = [];

    public int Released { get; set; }

    public Task<DeferredApplyResult> ApplyAsync(string marketplaceId, IReadOnlyCollection<DeferredTransactionItem> stillDeferred, DateTimeOffset seenAt, CancellationToken cancellationToken)
    {
        Applied.Add((marketplaceId, [.. stillDeferred]));
        return Task.FromResult(new DeferredApplyResult(stillDeferred.Count, Released));
    }
}
