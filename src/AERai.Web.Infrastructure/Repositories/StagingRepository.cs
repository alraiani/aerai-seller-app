using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Staging;
using AERai.Web.Infrastructure.Persistence;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IStagingRepository"/>.
/// </summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class StagingRepository(AppDbContext dbContext) : IStagingRepository
{
    /// <inheritdoc/>
    /// <remarks>
    /// EF batches the inserts; SaveChanges wraps the batch and its rows in one transaction.
    /// For very large files, SqlBulkCopy would be faster — revisit if uploads exceed ~100k rows.
    /// </remarks>
    public async Task<long> AddBatchAsync(ImportBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);

        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Staging rows are write-once; detaching keeps a large batch from lingering in the change tracker.
        dbContext.ChangeTracker.Clear();
        return batch.Id;
    }
}
