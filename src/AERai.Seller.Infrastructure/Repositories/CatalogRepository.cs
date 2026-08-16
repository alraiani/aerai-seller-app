using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain.Staging;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure.Repositories;

public sealed class CatalogRepository(IDbContextFactory<SellerDbContext> dbContextFactory) : ICatalogRepository
{
    public async Task UpsertAsync(
        IEnumerable<CatalogItem> items, IEnumerable<CatalogParent> parents, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Parents are saved first so items' ParentAsin FK always resolves to an existing row.
        foreach (var parent in parents)
        {
            var existing = await dbContext.CatalogParents.FindAsync([parent.ParentAsin], cancellationToken);
            if (existing is null)
            {
                dbContext.CatalogParents.Add(parent);
            }
            else
            {
                existing.Title = parent.Title;
                existing.SyncedAt = parent.SyncedAt;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var item in items)
        {
            var existing = await dbContext.CatalogItems.FindAsync([item.Asin], cancellationToken);
            if (existing is null)
            {
                dbContext.CatalogItems.Add(item);
            }
            else
            {
                existing.ParentAsin = item.ParentAsin;
                existing.Sku = item.Sku;
                existing.Title = item.Title;
                existing.Brand = item.Brand;
                existing.ImageUrl = item.ImageUrl;
                existing.SyncedAt = item.SyncedAt;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogItem>> GetAllItemsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.CatalogItems.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogParent>> GetAllParentsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.CatalogParents.AsNoTracking().ToListAsync(cancellationToken);
    }
}
