using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IAdSpendRepository"/> that records every range replaced.</summary>
internal sealed class FakeAdSpendRepository : IAdSpendRepository
{
    public List<(string MarketplaceId, DateOnly Start, DateOnly End, List<AdSpendRow> Rows, string DefaultCurrency)> Replaced { get; } = [];

    public Task<int> ReplaceRangeAsync(
        string marketplaceId, DateOnly start, DateOnly end, IReadOnlyCollection<AdSpendRow> rows, string defaultCurrency, DateTimeOffset syncedAt, CancellationToken cancellationToken)
    {
        Replaced.Add((marketplaceId, start, end, [.. rows], defaultCurrency));
        return Task.FromResult(rows.Count);
    }
}
