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

    /// <inheritdoc/>
    public Task<SettlementSummary?> GetSettlementAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken) =>
        dbContext.SettlementSummaries.AsNoTracking()
            .FirstOrDefaultAsync(s => s.MarketplaceId == marketplaceId && s.SettlementId == settlementId, cancellationToken);

    /// <inheritdoc/>
    public Task<PagedResult<SettlementSkuSummary>> GetSettlementSkuSummaryAsync(string marketplaceId, string settlementId, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.SettlementSkuSummaries.AsNoTracking()
            .Where(r => r.MarketplaceId == marketplaceId && r.SettlementId == settlementId);
        if (request.SafeSearch is { } search)
        {
            query = query.Where(r => (r.Sku != null && r.Sku.Contains(search))
                || (r.Asin != null && r.Asin.Contains(search))
                || (r.Title != null && r.Title.Contains(search)));
        }

        return query
            .OrderByDescending(r => r.Sales)
            .ThenBy(r => r.Sku)
            .ThenBy(r => r.SalePrice)
            .ToPagedResultAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SettlementSkuSummary?> GetSettlementSkuGroupAsync(string marketplaceId, string settlementId, string? sku, decimal? salePrice, CancellationToken cancellationToken) =>
        dbContext.SettlementSkuSummaries.AsNoTracking()
            .FirstOrDefaultAsync(r => r.MarketplaceId == marketplaceId && r.SettlementId == settlementId && r.Sku == sku && r.SalePrice == salePrice, cancellationToken);

    /// <inheritdoc/>
    public Task<PagedResult<SettlementLineDetail>> GetSettlementLinesAsync(string marketplaceId, string settlementId, string? sku, decimal? salePrice, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.SettlementLineDetails.AsNoTracking()
            .Where(l => l.MarketplaceId == marketplaceId && l.SettlementId == settlementId && l.Sku == sku && l.SalePrice == salePrice);
        if (request.SafeSearch is { } search)
        {
            query = query.Where(l => l.AmazonOrderId != null && l.AmazonOrderId.Contains(search));
        }

        return query
            .OrderBy(l => l.PostedDate)
            .ThenBy(l => l.AmazonOrderId)
            .ThenBy(l => l.LineId)
            .ToPagedResultAsync(request, cancellationToken);
    }
}
