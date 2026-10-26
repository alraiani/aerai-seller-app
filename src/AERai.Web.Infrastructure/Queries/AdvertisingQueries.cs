using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Advertising;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Queries;

/// <summary>EF Core implementation of <see cref="IAdvertisingQueries"/> over <c>core.AdSpend</c>, the catalog, and <c>rpt.vw_SalesLine</c>.</summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class AdvertisingQueries(AppDbContext dbContext) : IAdvertisingQueries
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<AdSkuRow>> GetSkuSpendAsync(string marketplaceId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var totals = await dbContext.AdSpends.AsNoTracking()
            .Where(s => s.MarketplaceId == marketplaceId && s.Date >= from && s.Date <= to)
            .GroupBy(s => s.Sku)
            .Select(g => new
            {
                Sku = g.Key,
                Asin = g.Max(s => s.Asin),
                Campaigns = g.Select(s => s.CampaignId).Distinct().Count(),
                Impressions = g.Sum(s => s.Impressions),
                Clicks = g.Sum(s => s.Clicks),
                Spend = g.Sum(s => s.Cost),
                AdSales = g.Sum(s => s.AttributedSales),
                AdOrders = g.Sum(s => s.AttributedOrders),
                AdUnits = g.Sum(s => s.AttributedUnits),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (totals.Count == 0)
        {
            return [];
        }

        var skus = totals.Select(t => t.Sku).ToList();
        var products = await (
                from p in dbContext.Products.AsNoTracking()
                where skus.Contains(p.Sku)
                join f in dbContext.ProductFamilies on p.FamilyId equals f.Id into families
                from f in families.DefaultIfEmpty()
                select new { p.Sku, p.Asin, p.Title, p.ImagePath, p.FamilyId, Family = f == null ? null : f.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var bySku = products.ToDictionary(p => p.Sku, StringComparer.OrdinalIgnoreCase);

        return [.. totals.Select(t =>
        {
            var product = bySku.GetValueOrDefault(t.Sku);
            return new AdSkuRow(
                t.Sku,
                product?.Asin ?? t.Asin,
                product?.Title,
                product?.ImagePath,
                product?.FamilyId,
                product?.Family,
                t.Campaigns,
                new AdMetrics(t.Impressions, t.Clicks, t.Spend, t.AdSales, t.AdOrders, t.AdUnits, 0m, 0));
        })];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, (int Units, decimal Sales)>> GetSalesBySkuAsync(
        string marketplaceId, DateTimeOffset from, DateTimeOffset before, IReadOnlyCollection<string> skus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skus);

        var list = skus.ToList();
        var sales = await dbContext.SalesLines.AsNoTracking()
            .Where(l => l.MarketplaceId == marketplaceId && l.PurchaseDate >= from && l.PurchaseDate < before && list.Contains(l.Sku))
            .GroupBy(l => l.Sku)
            .Select(g => new { Sku = g.Key, Units = g.Sum(l => l.Quantity), Sales = g.Sum(l => l.ItemPrice) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return sales.ToDictionary(s => s.Sku, s => (s.Units, s.Sales), StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public async Task<(DateOnly First, DateOnly Last)?> GetSpendRangeAsync(string marketplaceId, CancellationToken cancellationToken)
    {
        var range = await dbContext.AdSpends.AsNoTracking()
            .Where(s => s.MarketplaceId == marketplaceId)
            .GroupBy(_ => 1)
            .Select(g => new { First = g.Min(s => s.Date), Last = g.Max(s => s.Date) })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return range is null ? null : (range.First, range.Last);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AdFamilySpend>> GetFamilySpendAsync(string marketplaceId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        // Added up in SQL: a list of settlements can cover months of daily rows per SKU and campaign.
        var rows = await (
                from s in dbContext.AdSpends.AsNoTracking()
                where s.MarketplaceId == marketplaceId && s.Date >= @from && s.Date <= to
                join p in dbContext.Products on s.Sku equals p.Sku into products
                from p in products.DefaultIfEmpty()
                join f in dbContext.ProductFamilies on p.FamilyId equals f.Id into families
                from f in families.DefaultIfEmpty()
                group s.Cost by new { FamilyId = (int?)f.Id, Family = f.Name } into g
                select new { g.Key.FamilyId, g.Key.Family, Spend = g.Sum() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(r => new AdFamilySpend(r.FamilyId, r.Family, r.Spend))];
    }
}
