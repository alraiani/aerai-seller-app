using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IInventoryItemRepository"/>.
/// </summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class InventoryItemRepository(AppDbContext dbContext) : IInventoryItemRepository
{
    /// <inheritdoc/>
    public Task<InventoryItemDetails?> GetAsync(string sku, string marketplaceId, CancellationToken cancellationToken) =>
        (from p in dbContext.Products.AsNoTracking()
         where p.Sku == sku
         join f in dbContext.ProductFamilies on p.FamilyId equals f.Id into families
         from f in families.DefaultIfEmpty()
         join h in dbContext.HomeStocks.Where(h => h.MarketplaceId == marketplaceId) on p.Sku equals h.Sku into stock
         from h in stock.DefaultIfEmpty()
         join l in dbContext.LeadTimeProfiles.Where(l => l.MarketplaceId == marketplaceId) on p.Sku equals l.Sku into leadTimes
         from l in leadTimes.DefaultIfEmpty()
         select new InventoryItemDetails(
             p.Sku,
             p.Asin,
             p.Title,
             f == null ? null : f.Name,
             p.ImagePath,
             h == null ? 0 : h.Quantity,
             l == null
                 ? LeadTimeSettings.None
                 : new LeadTimeSettings(l.SupplierLeadTimeDays, l.PrepTimeDays, l.TransitDays, l.SafetyStockDays, l.TargetStockDays)))
        .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductFamily>> ListFamiliesAsync(CancellationToken cancellationToken) =>
        await dbContext.ProductFamilies.AsNoTracking().OrderBy(f => f.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<int> GetOrCreateFamilyAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // The column's collation is case-insensitive, so this equality matches "mats" to "Mats".
        var existing = await dbContext.ProductFamilies.Where(f => f.Name == name).Select(f => (int?)f.Id).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (existing is { } id)
        {
            return id;
        }

        var family = new ProductFamily { Name = name };
        dbContext.ProductFamilies.Add(family);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return family.Id;
        }
        catch (DbUpdateException)
        {
            // Another user created the same family at the same moment; use theirs.
            dbContext.Entry(family).State = EntityState.Detached;
            return await dbContext.ProductFamilies.Where(f => f.Name == name).Select(f => f.Id).SingleAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SetFamilyAsync(string sku, int? familyId, CancellationToken cancellationToken) =>
        await dbContext.Products.Where(p => p.Sku == sku)
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.FamilyId, familyId), cancellationToken)
            .ConfigureAwait(false) > 0;

    /// <inheritdoc/>
    public Task DeleteUnusedFamiliesAsync(CancellationToken cancellationToken) =>
        dbContext.ProductFamilies
            .Where(f => !dbContext.Products.Any(p => p.FamilyId == f.Id))
            .ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<(string Path, string ContentType)?> GetImageAsync(string sku, CancellationToken cancellationToken)
    {
        var image = await dbContext.Products.AsNoTracking()
            .Where(p => p.Sku == sku && p.ImagePath != null && p.ImageContentType != null)
            .Select(p => new { p.ImagePath, p.ImageContentType })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Both are non-null by the Where filter above.
        return image is null ? null : (image.ImagePath!, image.ImageContentType!);
    }

    /// <inheritdoc/>
    public async Task<bool> SetImageAsync(string sku, string? path, string? contentType, CancellationToken cancellationToken) =>
        await dbContext.Products.Where(p => p.Sku == sku)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.ImagePath, path)
                    .SetProperty(p => p.ImageContentType, contentType),
                cancellationToken)
            .ConfigureAwait(false) > 0;

    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>> GetExistingSkusAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skus);

        var found = new HashSet<string>(StringComparer.Ordinal);

        // Chunked so a large upload stays well under SQL Server's 2,100-parameter limit.
        foreach (var chunk in skus.Distinct(StringComparer.Ordinal).Chunk(1000))
        {
            found.UnionWith(await dbContext.Products.Where(p => chunk.Contains(p.Sku)).Select(p => p.Sku).ToListAsync(cancellationToken).ConfigureAwait(false));
        }

        return found;
    }

    /// <inheritdoc/>
    public async Task SetLeadTimesAsync(string sku, string marketplaceId, LeadTimeSettings settings, DateTimeOffset updatedAt, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var profiles = dbContext.LeadTimeProfiles.Where(p => p.Sku == sku && p.MarketplaceId == marketplaceId);

        // All blank means "use the defaults", which is represented by having no row.
        if (settings.IsEmpty)
        {
            await profiles.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var updated = await profiles
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.SupplierLeadTimeDays, settings.SupplierLeadTimeDays)
                    .SetProperty(p => p.PrepTimeDays, settings.PrepTimeDays)
                    .SetProperty(p => p.TransitDays, settings.TransitDays)
                    .SetProperty(p => p.SafetyStockDays, settings.SafetyStockDays)
                    .SetProperty(p => p.TargetStockDays, settings.TargetStockDays)
                    .SetProperty(p => p.UpdatedAt, updatedAt)
                    .SetProperty(p => p.UpdatedBy, updatedBy),
                cancellationToken)
            .ConfigureAwait(false);

        if (updated == 0)
        {
            dbContext.LeadTimeProfiles.Add(new LeadTimeProfile
            {
                Sku = sku,
                MarketplaceId = marketplaceId,
                SupplierLeadTimeDays = settings.SupplierLeadTimeDays,
                PrepTimeDays = settings.PrepTimeDays,
                TransitDays = settings.TransitDays,
                SafetyStockDays = settings.SafetyStockDays,
                TargetStockDays = settings.TargetStockDays,
                UpdatedAt = updatedAt,
                UpdatedBy = updatedBy,
            });
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task SetHomeStockAsync(string marketplaceId, IReadOnlyList<HomeStockEntry> entries, DateTimeOffset updatedAt, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var skus = entries.Select(e => e.Sku).ToList();
            var existing = new Dictionary<string, HomeStock>(StringComparer.Ordinal);
            foreach (var chunk in skus.Chunk(1000))
            {
                foreach (var row in await dbContext.HomeStocks.Where(h => h.MarketplaceId == marketplaceId && chunk.Contains(h.Sku)).ToListAsync(cancellationToken).ConfigureAwait(false))
                {
                    existing[row.Sku] = row;
                }
            }

            foreach (var entry in entries)
            {
                existing.TryGetValue(entry.Sku, out var row);

                // Zero is stored as "no row", the same way an unset cost of goods is.
                if (entry.Quantity == 0)
                {
                    if (row is not null)
                    {
                        dbContext.HomeStocks.Remove(row);
                    }
                }
                else if (row is null)
                {
                    dbContext.HomeStocks.Add(new HomeStock { Sku = entry.Sku, MarketplaceId = marketplaceId, Quantity = entry.Quantity, UpdatedAt = updatedAt, UpdatedBy = updatedBy });
                }
                else if (row.Quantity != entry.Quantity)
                {
                    row.Quantity = entry.Quantity;
                    row.UpdatedAt = updatedAt;
                    row.UpdatedBy = updatedBy;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
