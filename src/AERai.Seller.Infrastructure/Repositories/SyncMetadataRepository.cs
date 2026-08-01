using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure.Repositories;

public sealed class SyncMetadataRepository(IDbContextFactory<SellerDbContext> dbContextFactory) : ISyncMetadataRepository
{
    public async Task<SyncMetadata?> GetAsync(string syncJobName, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.SyncMetadata.AsNoTracking().SingleOrDefaultAsync(s => s.SyncJobName == syncJobName, cancellationToken);
    }

    public async Task<IReadOnlyList<SyncMetadata>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.SyncMetadata.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task RecordResultAsync(string syncJobName, bool succeeded, string? errorMessage, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await dbContext.SyncMetadata.FindAsync([syncJobName], cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            dbContext.SyncMetadata.Add(new SyncMetadata
            {
                SyncJobName = syncJobName,
                LastSuccessfulSyncAt = succeeded ? now : DateTimeOffset.MinValue,
                LastSyncSucceeded = succeeded,
                LastErrorMessage = errorMessage,
            });
        }
        else
        {
            if (succeeded)
            {
                existing.LastSuccessfulSyncAt = now;
            }
            existing.LastSyncSucceeded = succeeded;
            existing.LastErrorMessage = errorMessage;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
