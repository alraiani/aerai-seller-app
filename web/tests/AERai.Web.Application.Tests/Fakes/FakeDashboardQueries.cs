using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Dashboard;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IDashboardQueries"/> seeded by each test.</summary>
internal sealed class FakeDashboardQueries : IDashboardQueries
{
    public List<SalesLine> Lines { get; } = [];

    public List<InventoryPosition> Positions { get; } = [];

    public SettlementSummary? LatestSettlement { get; set; }

    public List<string> MissingCost { get; } = [];

    public int AwaitingPromotion { get; set; }

    public List<SyncGlance> Sync { get; } = [];

    public DateTimeOffset? CoverageStart { get; set; }

    /// <summary>The window the service asked for, to assert it fetches enough history.</summary>
    public (DateTimeOffset From, DateTimeOffset To)? RequestedWindow { get; private set; }

    public Task<IReadOnlyList<SalesLine>> GetSalesLinesAsync(DateTimeOffset from, DateTimeOffset to, string currency, CancellationToken cancellationToken)
    {
        RequestedWindow = (from, to);
        return Task.FromResult<IReadOnlyList<SalesLine>>(Lines.Where(l => l.PurchaseDate >= from && l.PurchaseDate < to).ToList());
    }

    public Task<IReadOnlyList<InventoryPosition>> GetInventoryPositionsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<InventoryPosition>>(Positions);

    public Task<SettlementSummary?> GetLatestSettlementAsync(string currency, CancellationToken cancellationToken) =>
        Task.FromResult(LatestSettlement);

    public Task<IReadOnlyList<string>> GetSoldSkusMissingCostAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(MissingCost);

    public Task<int> CountBatchesAwaitingPromotionAsync(CancellationToken cancellationToken) => Task.FromResult(AwaitingPromotion);

    public Task<IReadOnlyList<SyncGlance>> GetSyncHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SyncGlance>>(Sync);

    public Task<DateTimeOffset?> GetOrdersCoverageStartAsync(CancellationToken cancellationToken) => Task.FromResult(CoverageStart);
}
