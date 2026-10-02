using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Reporting;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Queries;

/// <summary>
/// EF Core implementation of <see cref="IReportingQueries"/> over the <c>rpt</c> views.
/// </summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class ReportingQueries(AppDbContext dbContext) : IReportingQueries
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<DailySalesBySku>> GetDailySalesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await dbContext.DailySalesBySku
            .AsNoTracking()
            .Where(s => s.SalesDate >= from && s.SalesDate <= to)
            .OrderBy(s => s.SalesDate)
            .ThenBy(s => s.Sku)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public Task<PagedResult<OrderSummary>> GetOrdersAsync(PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.OrderSummaries.AsNoTracking();
        if (request.SafeSearch is { } search)
        {
            query = query.Where(o => o.AmazonOrderId.Contains(search) || o.OrderStatus.Contains(search));
        }

        return query
            .OrderByDescending(o => o.PurchaseDate)
            .ThenBy(o => o.AmazonOrderId)
            .ToPagedResultAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PagedResult<InventoryPosition>> GetInventoryPositionsAsync(PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.InventoryPositions.AsNoTracking();
        if (request.SafeSearch is { } search)
        {
            query = query.Where(p => p.Sku.Contains(search) || (p.Title != null && p.Title.Contains(search)));
        }

        // SKUs with no sales (NULL days of supply) sort last: they're not at risk of stocking out.
        return query
            .OrderBy(p => p.DaysOfSupply == null)
            .ThenBy(p => p.DaysOfSupply)
            .ThenBy(p => p.Sku)
            .ToPagedResultAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InventoryPosition>> GetAtRiskInventoryAsync(decimal maxDaysOfSupply, int take, CancellationToken cancellationToken) =>
        await dbContext.InventoryPositions
            .AsNoTracking()
            .Where(p => p.DaysOfSupply != null && p.DaysOfSupply <= maxDaysOfSupply)
            .OrderBy(p => p.DaysOfSupply)
            .ThenBy(p => p.Sku)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public Task<PagedResult<SettlementSummary>> GetSettlementsAsync(PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.SettlementSummaries.AsNoTracking();
        if (request.SafeSearch is { } search)
        {
            query = query.Where(s => s.SettlementId.Contains(search));
        }

        return query
            .OrderByDescending(s => s.PeriodEnd)
            .ThenBy(s => s.SettlementId)
            .ToPagedResultAsync(request, cancellationToken);
    }
}
