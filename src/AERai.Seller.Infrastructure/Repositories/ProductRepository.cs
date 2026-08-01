using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure.Repositories;

public sealed class ProductRepository(IDbContextFactory<SellerDbContext> dbContextFactory) : IProductRepository
{
    public async Task UpsertAsync(Product product, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await dbContext.Products.FindAsync([product.Sku], cancellationToken);
        if (existing is null)
        {
            dbContext.Products.Add(product);
        }
        else
        {
            existing.Asin = product.Asin;
            existing.Title = product.Title;
            existing.CostOfGoods = product.CostOfGoods;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Products.AsNoTracking().ToListAsync(cancellationToken);
    }
}
