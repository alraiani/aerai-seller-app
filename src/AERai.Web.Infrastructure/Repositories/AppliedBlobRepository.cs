using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IAppliedBlobRepository"/>.</summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class AppliedBlobRepository(AppDbContext dbContext) : IAppliedBlobRepository
{
    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>> GetLoadedPathsAsync(CancellationToken cancellationToken)
    {
        var applied = await dbContext.AppliedBlobs.AsNoTracking().Select(b => b.Path).ToListAsync(cancellationToken).ConfigureAwait(false);
        var staged = await dbContext.ImportBatches.AsNoTracking()
            .Where(b => b.RawFilePath != null)
            .Select(b => b.RawFilePath!) // Non-null: filtered just above.
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new HashSet<string>(applied.Concat(staged), StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public async Task MarkAppliedAsync(AppliedBlob blob, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(blob);

        if (await dbContext.AppliedBlobs.AnyAsync(b => b.Path == blob.Path, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        dbContext.AppliedBlobs.Add(blob);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();
    }
}
