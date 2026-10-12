using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISyncSettingsRepository"/> over the single
/// <c>ops.SyncSettings</c> row (seeded by migration).
/// </summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class SyncSettingsRepository(AppDbContext dbContext) : ISyncSettingsRepository
{
    /// <inheritdoc/>
    public async Task<SyncSettings> GetAsync(CancellationToken cancellationToken) =>
        await dbContext.SyncSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == SyncSettings.SingletonId, cancellationToken).ConfigureAwait(false)
        ?? new SyncSettings();

    /// <inheritdoc/>
    public Task SetPausedAsync(bool paused, string user, DateTimeOffset at, CancellationToken cancellationToken) =>
        dbContext.SyncSettings
            .Where(s => s.Id == SyncSettings.SingletonId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.IsPaused, paused)
                .SetProperty(s => s.PausedAt, paused ? at : null)
                .SetProperty(s => s.PausedBy, paused ? user : null),
                cancellationToken);
}
