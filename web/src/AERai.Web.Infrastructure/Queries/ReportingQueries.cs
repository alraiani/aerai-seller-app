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
    public Task<PagedResult<OrderSummary>> GetOrdersAsync(string marketplaceId, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.OrderSummaries.AsNoTracking().Where(v => v.MarketplaceId == marketplaceId);
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
    public Task<PagedResult<InventoryPosition>> GetInventoryPositionsAsync(string marketplaceId, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.InventoryPositions.AsNoTracking().Where(v => v.MarketplaceId == marketplaceId);
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
    public Task<PagedResult<SettlementSummary>> GetSettlementsAsync(string marketplaceId, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.SettlementSummaries.AsNoTracking().Where(v => v.MarketplaceId == marketplaceId);
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
