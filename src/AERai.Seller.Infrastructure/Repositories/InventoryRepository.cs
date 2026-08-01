using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure.Repositories;

public sealed class InventoryRepository(IDbContextFactory<SellerDbContext> dbContextFactory) : IInventoryRepository
{
    public async Task UpsertSnapshotsAsync(IEnumerable<InventorySnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        foreach (var snapshot in snapshots)
        {
            var existing = await dbContext.InventorySnapshots.SingleOrDefaultAsync(
                s => s.Sku == snapshot.Sku && s.State == snapshot.State && s.SnapshotDate == snapshot.SnapshotDate,
                cancellationToken);

            if (existing is null)
            {
                dbContext.InventorySnapshots.Add(snapshot);
            }
            else
            {
                existing.Quantity = snapshot.Quantity;
                existing.SyncedAt = snapshot.SyncedAt;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventorySnapshot>> GetLatestSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Latest snapshot per (Sku, State): the most recent SnapshotDate for that pair.
        var latestDates = dbContext.InventorySnapshots
            .GroupBy(s => new { s.Sku, s.State })
            .Select(g => new { g.Key.Sku, g.Key.State, MaxDate = g.Max(s => s.SnapshotDate) });

        var query =
            from snapshot in dbContext.InventorySnapshots
            join latest in latestDates
                on new { snapshot.Sku, snapshot.State, Date = snapshot.SnapshotDate }
                equals new { latest.Sku, latest.State, Date = latest.MaxDate }
            select snapshot;

        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }
}
