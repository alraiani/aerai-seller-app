using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IAwdInventoryRepository"/>.</summary>
/// <remarks>
/// AWD stock is not an input to stock status (sell-through stock is FBA only), so writing it does not
/// ask for a stock-alert refresh.
/// </remarks>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class AwdInventoryRepository(AppDbContext dbContext) : IAwdInventoryRepository
{
    /// <inheritdoc/>
    public async Task<int> ReplaceSnapshotAsync(
        string marketplaceId, DateOnly snapshotDate, IReadOnlyCollection<AwdInventoryItem> items, DateTimeOffset syncedAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(items);

        var skus = items.Select(i => i.Sku).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (skus.Count != items.Count)
        {
            throw new ArgumentException("Each SKU may appear only once.", nameof(items));
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            // Snapshots reference the shared catalog, so a SKU seen only in AWD so far joins it, as
            // promotion does for SKUs first seen in an FBA report.
            var known = await dbContext.Products
                .Where(p => skus.Contains(p.Sku))
                .Select(p => p.Sku)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var knownSet = known.ToHashSet(StringComparer.OrdinalIgnoreCase);
            dbContext.Products.AddRange(skus
                .Where(sku => !knownSet.Contains(sku))
                .Select(sku => new Product { Sku = sku, CreatedAt = syncedAt, UpdatedAt = syncedAt }));

            // Amazon's listing is the whole of AWD, so the date's rows are replaced rather than merged:
            // a SKU that left AWD since an earlier run that day must not linger.
            await dbContext.AwdInventorySnapshots
                .Where(s => s.MarketplaceId == marketplaceId && s.SnapshotDate == snapshotDate)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            // Use the catalog's spelling of each SKU so the foreign key and view joins match exactly.
            var catalogSpelling = known.ToDictionary(sku => sku, StringComparer.OrdinalIgnoreCase);
            dbContext.AwdInventorySnapshots.AddRange(items.Select(i => new AwdInventorySnapshot
            {
                MarketplaceId = marketplaceId,
                Sku = catalogSpelling.GetValueOrDefault(i.Sku, i.Sku),
                SnapshotDate = snapshotDate,
                OnHand = i.OnHand,
                Inbound = i.Inbound,
                AvailableDistributable = i.AvailableDistributable,
                ReservedDistributable = i.ReservedDistributable,
                Replenishment = i.Replenishment,
                SyncedAt = syncedAt,
            }));

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            return items.Count;
        }).ConfigureAwait(false);
    }
}
