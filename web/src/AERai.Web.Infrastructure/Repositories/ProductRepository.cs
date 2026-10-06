using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Products;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using AERai.Web.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IProductRepository"/>.
/// </summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class ProductRepository(AppDbContext dbContext) : IProductRepository
{
    /// <inheritdoc/>
    public async Task<PagedResult<ProductSummary>> ListAsync(string marketplaceId, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.Products.AsNoTracking();
        if (request.SafeSearch is { } search)
        {
            query = query.Where(p => p.Sku.Contains(search) || (p.Asin != null && p.Asin.Contains(search)) || (p.Title != null && p.Title.Contains(search)));
        }

        return await WithCost(query, marketplaceId)
            .ToPagedResultAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<ProductSummary?> GetAsync(string sku, string marketplaceId, CancellationToken cancellationToken) =>
        WithCost(dbContext.Products.AsNoTracking().Where(p => p.Sku == sku), marketplaceId)
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> UpdateCostAsync(string sku, string marketplaceId, decimal? costOfGoods, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        if (!await dbContext.Products.AnyAsync(p => p.Sku == sku, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        // Set-based writes: one round trip each, no tracked entity. Clearing a cost deletes its row,
        // because "no row" is how an unset cost is represented.
        var costs = dbContext.ProductCosts.Where(c => c.Sku == sku && c.MarketplaceId == marketplaceId);
        if (costOfGoods is null)
        {
            await costs.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        var updated = await costs
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(c => c.CostOfGoods, costOfGoods.Value)
                    .SetProperty(c => c.UpdatedAt, updatedAt),
                cancellationToken)
            .ConfigureAwait(false);

        if (updated == 0)
        {
            dbContext.ProductCosts.Add(new ProductCost { Sku = sku, MarketplaceId = marketplaceId, CostOfGoods = costOfGoods.Value, UpdatedAt = updatedAt });
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Projects products, ordered by SKU, with their cost in one marketplace (left join: no row means
    /// not set). Ordering happens before the projection because EF cannot order by a record's member.
    /// </summary>
    private IQueryable<ProductSummary> WithCost(IQueryable<Product> products, string marketplaceId) =>
        from p in products
        join c in dbContext.ProductCosts.Where(c => c.MarketplaceId == marketplaceId) on p.Sku equals c.Sku into costs
        from c in costs.DefaultIfEmpty()
        orderby p.Sku
        select new ProductSummary(
            p.Sku,
            p.Asin,
            p.Title,
            c == null ? null : c.CostOfGoods,
            c == null || c.UpdatedAt < p.UpdatedAt ? p.UpdatedAt : c.UpdatedAt);
}
