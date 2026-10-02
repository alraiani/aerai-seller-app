using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Products;
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
    public async Task<PagedResult<ProductSummary>> ListAsync(PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.Products.AsNoTracking();
        if (request.SafeSearch is { } search)
        {
            query = query.Where(p => p.Sku.Contains(search) || (p.Asin != null && p.Asin.Contains(search)) || (p.Title != null && p.Title.Contains(search)));
        }

        return await query
            .OrderBy(p => p.Sku)
            .Select(p => new ProductSummary(p.Sku, p.Asin, p.Title, p.CostOfGoods, p.UpdatedAt))
            .ToPagedResultAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<ProductSummary?> GetAsync(string sku, CancellationToken cancellationToken) =>
        dbContext.Products
            .AsNoTracking()
            .Where(p => p.Sku == sku)
            .Select(p => new ProductSummary(p.Sku, p.Asin, p.Title, p.CostOfGoods, p.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> UpdateCostAsync(string sku, decimal? costOfGoods, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        // Set-based update: one round trip, no tracked entity, and promotion's concurrent writes to
        // other columns (Title/Asin) are not overwritten.
        var affected = await dbContext.Products
            .Where(p => p.Sku == sku)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.CostOfGoods, costOfGoods)
                    .SetProperty(p => p.UpdatedAt, updatedAt),
                cancellationToken)
            .ConfigureAwait(false);

        return affected == 1;
    }
}
