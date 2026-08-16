using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain.Staging;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure.Repositories;

public sealed class AwdInventoryRepository(IDbContextFactory<SellerDbContext> dbContextFactory) : IAwdInventoryRepository
{
    public async Task UpsertSnapshotsAsync(IEnumerable<AwdInventorySnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        foreach (var snapshot in snapshots)
        {
            var existing = await dbContext.AwdInventorySnapshots.SingleOrDefaultAsync(
                s => s.Sku == snapshot.Sku && s.SnapshotDate == snapshot.SnapshotDate,
                cancellationToken);

            if (existing is null)
            {
                dbContext.AwdInventorySnapshots.Add(snapshot);
            }
            else
            {
                existing.TotalOnhandQuantity = snapshot.TotalOnhandQuantity;
                existing.TotalInboundQuantity = snapshot.TotalInboundQuantity;
                existing.AvailableDistributableQuantity = snapshot.AvailableDistributableQuantity;
                existing.ReservedDistributableQuantity = snapshot.ReservedDistributableQuantity;
                existing.ReplenishmentQuantity = snapshot.ReplenishmentQuantity;
                existing.SyncedAt = snapshot.SyncedAt;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AwdInventorySnapshot>> GetLatestSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Latest snapshot per Sku: the most recent SnapshotDate for that SKU.
        var latestDates = dbContext.AwdInventorySnapshots
            .GroupBy(s => s.Sku)
            .Select(g => new { Sku = g.Key, MaxDate = g.Max(s => s.SnapshotDate) });

        var query =
            from snapshot in dbContext.AwdInventorySnapshots
            join latest in latestDates
                on new { snapshot.Sku, Date = snapshot.SnapshotDate }
                equals new { latest.Sku, Date = latest.MaxDate }
            select snapshot;

        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }
}
