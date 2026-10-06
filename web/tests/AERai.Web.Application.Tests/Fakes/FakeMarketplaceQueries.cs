using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory marketplace list (the seeded three by default).</summary>
internal sealed class FakeMarketplaceQueries : IMarketplaceQueries
{
    public List<Marketplace> Marketplaces { get; } = [.. TestMarketplaces.All];

    public Task<IReadOnlyList<Marketplace>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Marketplace>>(Marketplaces.OrderBy(m => m.SortOrder).ToList());
}
