using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IAdSpendRepository"/>.</summary>
/// <remarks>Ad spend is not an input to stock status, so writing it does not ask for a stock-alert refresh.</remarks>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class AdSpendRepository(AppDbContext dbContext) : IAdSpendRepository
{
    /// <inheritdoc/>
    public async Task<int> ReplaceRangeAsync(
        string marketplaceId, DateOnly start, DateOnly end, IReadOnlyCollection<AdSpendRow> rows, string defaultCurrency, DateTimeOffset syncedAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCurrency);
        ArgumentNullException.ThrowIfNull(rows);
        if (end < start)
        {
            throw new ArgumentException("The range ends before it starts.", nameof(end));
        }

        // Rows outside the range would survive the next replace of their own days twice over; refuse them.
        if (rows.Any(r => r.Date < start || r.Date > end))
        {
            throw new ArgumentException("Every row must fall inside the replaced range.", nameof(rows));
        }

        var skus = rows.Select(r => r.Sku).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            // Spend references the shared catalog, so a SKU seen only in advertising so far joins it, as
            // promotion does for SKUs first seen in a report.
            var known = await dbContext.Products
                .Where(p => skus.Contains(p.Sku))
                .Select(p => p.Sku)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var knownSet = known.ToHashSet(StringComparer.OrdinalIgnoreCase);
            dbContext.Products.AddRange(skus
                .Where(sku => !knownSet.Contains(sku))
                .Select(sku => new Product
                {
                    Sku = sku,
                    Asin = rows.FirstOrDefault(r => string.Equals(r.Sku, sku, StringComparison.OrdinalIgnoreCase) && r.Asin is not null)?.Asin,
                    CreatedAt = syncedAt,
                    UpdatedAt = syncedAt,
                }));

            // Amazon's report is the whole of the range's spend and revises attributed sales for days after,
            // so the range is replaced rather than merged: a campaign that no longer shows must not linger.
            await dbContext.AdSpends
                .Where(s => s.MarketplaceId == marketplaceId && s.Date >= start && s.Date <= end)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // Use the catalog's spelling of each SKU so the foreign key and joins match exactly.
            var catalogSpelling = known.ToDictionary(sku => sku, StringComparer.OrdinalIgnoreCase);
            dbContext.AdSpends.AddRange(rows.Select(r => new AdSpend
            {
                MarketplaceId = marketplaceId,
                Date = r.Date,
                Sku = catalogSpelling.GetValueOrDefault(r.Sku, r.Sku),
                Asin = r.Asin,
                CampaignId = r.CampaignId,
                CampaignName = r.CampaignName is { Length: > 256 } name ? name[..256] : r.CampaignName,
                Impressions = r.Impressions,
                Clicks = r.Clicks,
                Cost = r.Cost,
                AttributedSales = r.AttributedSales,
                AttributedOrders = r.AttributedOrders,
                AttributedUnits = r.AttributedUnits,
                Currency = r.Currency ?? defaultCurrency,
                SyncedAt = syncedAt,
            }));

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            return rows.Count;
        }).ConfigureAwait(false);
    }
}
