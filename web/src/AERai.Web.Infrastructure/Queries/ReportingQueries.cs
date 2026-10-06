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
