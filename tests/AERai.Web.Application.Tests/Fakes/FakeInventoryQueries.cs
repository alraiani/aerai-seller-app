using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IInventoryQueries"/> seeded by each test.</summary>
internal sealed class FakeInventoryQueries : IInventoryQueries
{
    public List<InventoryPosition> Positions { get; } = [];

    public List<UnitsSold> Sold { get; } = [];

    public Dictionary<string, LeadTimeSettings> LeadTimes { get; } = new(StringComparer.Ordinal);

    /// <summary>The lower bound the service asked sales for, to assert it fetches the whole window.</summary>
    public DateTimeOffset? RequestedSince { get; private set; }

    public Task<IReadOnlyList<InventoryPosition>> GetPositionsAsync(string marketplaceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<InventoryPosition>>(Positions.Where(p => p.MarketplaceId == marketplaceId).ToList());

    public Task<IReadOnlyList<UnitsSold>> GetUnitsSoldAsync(string marketplaceId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        RequestedSince = since;
        return Task.FromResult<IReadOnlyList<UnitsSold>>(Sold.Where(s => s.PurchaseDate >= since).ToList());
    }

    public Task<IReadOnlyDictionary<string, LeadTimeSettings>> GetLeadTimesAsync(string marketplaceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, LeadTimeSettings>>(LeadTimes);
}
