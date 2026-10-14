using System.Data;
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
    public async Task<PagedResult<ProductSummary>> ListAsync(string marketplaceId, ProductListFilter filter, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(request);

        var rows = Matching(marketplaceId, filter.FamilyId, request.SafeSearch);
        rows = filter.Cost switch
        {
            ProductCostFilter.Missing => rows.Where(r => r.CostOfGoods == null),
            ProductCostFilter.Set => rows.Where(r => r.CostOfGoods != null),
            _ => rows,
        };

        return await Sort(rows, filter.Sort, filter.Sort.IsDescendingByDefault() != filter.Descending)
            .Select(r => new ProductSummary(r.Sku, r.Asin, r.Title, r.CostOfGoods, r.UpdatedAt, r.FamilyId, r.Family, r.ImagePath, r.CostUpdatedAt))
            .ToPagedResultAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ProductListTotals> GetTotalsAsync(string marketplaceId, int? familyId, string? search, CancellationToken cancellationToken)
    {
        var counts = await Matching(marketplaceId, familyId, search)
            .GroupBy(_ => 1)
            .Select(g => new { Skus = g.Count(), WithCost = g.Count(r => r.CostOfGoods != null) })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return counts is null ? new ProductListTotals(0, 0) : new ProductListTotals(counts.Skus, counts.WithCost);
    }

    /// <inheritdoc/>
    public Task<ProductSummary?> GetAsync(string sku, string marketplaceId, CancellationToken cancellationToken) =>
        Rows(dbContext.Products.AsNoTracking().Where(p => p.Sku == sku), marketplaceId)
            .Select(r => new ProductSummary(r.Sku, r.Asin, r.Title, r.CostOfGoods, r.UpdatedAt, r.FamilyId, r.Family, r.ImagePath, r.CostUpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, ProductCostSnapshot>> GetCostsAsync(string marketplaceId, IReadOnlyCollection<string> skus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skus);
        if (skus.Count == 0)
        {
            return new Dictionary<string, ProductCostSnapshot>(StringComparer.Ordinal);
        }

        var rows = await Rows(dbContext.Products.AsNoTracking().Where(p => skus.Contains(p.Sku)), marketplaceId)
            .Select(r => new { r.Sku, r.Title, r.CostOfGoods })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // SQL Server compares SKUs ignoring case, but the sheet's SKU must match exactly to be applied.
        return rows.ToDictionary(r => r.Sku, r => new ProductCostSnapshot(r.Title, r.CostOfGoods), StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductCostTemplateRow>> ListForCostSheetAsync(string marketplaceId, int? familyId, CancellationToken cancellationToken) =>
        await Matching(marketplaceId, familyId, search: null)
            .OrderBy(r => r.Family == null)
            .ThenBy(r => r.Family)
            .ThenBy(r => r.Sku)
            .Select(r => new ProductCostTemplateRow(r.Sku, r.CostOfGoods, r.Asin, r.Title, r.Family, r.CostUpdatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ApplyCostsAsync(string marketplaceId, IReadOnlyList<ProductCostChange> changes, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var skus = changes.Select(c => c.Sku).ToList();
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();

            // Serializable: each change is checked against the cost read here, so a concurrent save
            // can't slip in between the check and the write and be overwritten unseen.
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

            var known = (await dbContext.Products.Where(p => skus.Contains(p.Sku)).Select(p => p.Sku).ToListAsync(cancellationToken).ConfigureAwait(false))
                .ToHashSet(StringComparer.Ordinal);
            var costs = (await dbContext.ProductCosts.Where(c => c.MarketplaceId == marketplaceId && skus.Contains(c.Sku)).ToListAsync(cancellationToken).ConfigureAwait(false))
                .ToDictionary(c => c.Sku, StringComparer.Ordinal);

            var stale = new List<string>();
            foreach (var change in changes)
            {
                var existing = costs.GetValueOrDefault(change.Sku);
                if (!known.Contains(change.Sku) || existing?.CostOfGoods != change.Current)
                {
                    stale.Add(change.Sku);
                    continue;
                }

                // No row is how an unset cost is represented, so clearing deletes it. A clear always has
                // a current cost (no-op changes are dropped by the service) and that cost matched the
                // stored row just above, so the row exists.
                if (change.New is not { } cost)
                {
                    dbContext.ProductCosts.Remove(existing!);
                }
                else if (existing is null)
                {
                    dbContext.ProductCosts.Add(new ProductCost { Sku = change.Sku, MarketplaceId = marketplaceId, CostOfGoods = cost, UpdatedAt = updatedAt });
                }
                else
                {
                    existing.CostOfGoods = cost;
                    existing.UpdatedAt = updatedAt;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return (IReadOnlyList<string>)stale;
        }).ConfigureAwait(false);
    }

    /// <summary>Products matching the family and every search word, with their cost in one marketplace.</summary>
    private IQueryable<Row> Matching(string marketplaceId, int? familyId, string? search)
    {
        var rows = Rows(dbContext.Products.AsNoTracking(), marketplaceId);
        if (familyId is { } family)
        {
            rows = rows.Where(r => r.FamilyId == family);
        }

        foreach (var term in ProductSearch.Terms(search))
        {
            rows = rows.Where(r => r.Sku.Contains(term)
                || (r.Asin != null && r.Asin.Contains(term))
                || (r.Title != null && r.Title.Contains(term))
                || (r.Family != null && r.Family.Contains(term)));
        }

        return rows;
    }

    /// <summary>
    /// Products with their family and their cost in one marketplace (left joins: no cost row means not
    /// set). Projected into <see cref="Row"/> so filters and sorts compose before the final record,
    /// because EF cannot order by a record's constructor arguments.
    /// </summary>
    private IQueryable<Row> Rows(IQueryable<Product> products, string marketplaceId) =>
        from p in products
        join c in dbContext.ProductCosts.Where(c => c.MarketplaceId == marketplaceId) on p.Sku equals c.Sku into costs
        from c in costs.DefaultIfEmpty()
        join f in dbContext.ProductFamilies on p.FamilyId equals f.Id into families
        from f in families.DefaultIfEmpty()
        select new Row
        {
            Sku = p.Sku,
            Asin = p.Asin,
            Title = p.Title,
            FamilyId = p.FamilyId,
            Family = f == null ? null : f.Name,
            ImagePath = p.ImagePath,
            CostOfGoods = c == null ? null : c.CostOfGoods,
            CostUpdatedAt = c == null ? null : c.UpdatedAt,
            UpdatedAt = c == null || c.UpdatedAt < p.UpdatedAt ? p.UpdatedAt : c.UpdatedAt,
        };

    /// <summary>Orders rows; blanks (no title, family, or cost) always go last, and SKU breaks ties so paging is stable.</summary>
    private static IQueryable<Row> Sort(IQueryable<Row> rows, ProductSort sort, bool descending) => sort switch
    {
        ProductSort.Title => descending
            ? rows.OrderBy(r => r.Title == null).ThenByDescending(r => r.Title).ThenBy(r => r.Sku)
            : rows.OrderBy(r => r.Title == null).ThenBy(r => r.Title).ThenBy(r => r.Sku),
        ProductSort.Family => descending
            ? rows.OrderBy(r => r.Family == null).ThenByDescending(r => r.Family).ThenBy(r => r.Sku)
            : rows.OrderBy(r => r.Family == null).ThenBy(r => r.Family).ThenBy(r => r.Sku),
        ProductSort.Cost => descending
            ? rows.OrderBy(r => r.CostOfGoods == null).ThenByDescending(r => r.CostOfGoods).ThenBy(r => r.Sku)
            : rows.OrderBy(r => r.CostOfGoods == null).ThenBy(r => r.CostOfGoods).ThenBy(r => r.Sku),
        ProductSort.Updated => descending
            ? rows.OrderByDescending(r => r.UpdatedAt).ThenBy(r => r.Sku)
            : rows.OrderBy(r => r.UpdatedAt).ThenBy(r => r.Sku),
        _ => descending ? rows.OrderByDescending(r => r.Sku) : rows.OrderBy(r => r.Sku),
    };

    /// <summary>One product row before it becomes a <see cref="ProductSummary"/>.</summary>
    private sealed class Row
    {
        public required string Sku { get; init; }

        public string? Asin { get; init; }

        public string? Title { get; init; }

        public int? FamilyId { get; init; }

        public string? Family { get; init; }

        public string? ImagePath { get; init; }

        public decimal? CostOfGoods { get; init; }

        public DateTimeOffset? CostUpdatedAt { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }
    }
}
