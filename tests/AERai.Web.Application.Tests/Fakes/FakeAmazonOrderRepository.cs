using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IAmazonOrderRepository"/> that remembers which order ids it has seen.</summary>
internal sealed class FakeAmazonOrderRepository : IAmazonOrderRepository
{
    private readonly HashSet<string> _known = new(StringComparer.Ordinal);

    public List<(string MarketplaceId, List<AmazonOrder> Orders)> Writes { get; } = [];

    public Task<OrderUpsertResult> UpsertAsync(string marketplaceId, IReadOnlyCollection<AmazonOrder> orders, DateTimeOffset syncedAt, CancellationToken cancellationToken)
    {
        Writes.Add((marketplaceId, [.. orders]));
        var inserted = orders.Count(o => _known.Add(o.AmazonOrderId));
        return Task.FromResult(new OrderUpsertResult(inserted, orders.Count - inserted));
    }
}
