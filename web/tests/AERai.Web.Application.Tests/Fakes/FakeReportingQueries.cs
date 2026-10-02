using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IReportingQueries"/> seeded with rows by each test.</summary>
internal sealed class FakeReportingQueries : IReportingQueries
{
    public List<DailySalesBySku> DailySales { get; } = [];

    public List<InventoryPosition> Positions { get; } = [];

    public Task<IReadOnlyList<DailySalesBySku>> GetDailySalesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DailySalesBySku>>(DailySales.Where(s => s.SalesDate >= from && s.SalesDate <= to).ToList());

    public Task<IReadOnlyList<InventoryPosition>> GetAtRiskInventoryAsync(decimal maxDaysOfSupply, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<InventoryPosition>>(
            Positions.Where(p => p.DaysOfSupply <= maxDaysOfSupply).OrderBy(p => p.DaysOfSupply).Take(take).ToList());

    public Task<PagedResult<OrderSummary>> GetOrdersAsync(PageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<PagedResult<InventoryPosition>> GetInventoryPositionsAsync(PageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<PagedResult<SettlementSummary>> GetSettlementsAsync(PageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
