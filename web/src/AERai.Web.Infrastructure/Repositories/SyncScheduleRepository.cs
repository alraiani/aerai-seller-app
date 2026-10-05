using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISyncScheduleRepository"/>.
/// </summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class SyncScheduleRepository(AppDbContext dbContext) : ISyncScheduleRepository
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<SyncSchedule>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.SyncSchedules.AsNoTracking().OrderBy(s => s.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public Task<SyncSchedule?> GetAsync(int id, CancellationToken cancellationToken) =>
        dbContext.SyncSchedules.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<int> AddAsync(SyncSchedule schedule, CancellationToken cancellationToken)
    {
        dbContext.SyncSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        dbContext.Entry(schedule).State = EntityState.Detached;
        return schedule.Id;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateSettingsAsync(SyncSchedule schedule, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        // Explicit column list: never overwrites LastRunAt, which the scheduler may be updating concurrently.
        var affected = await dbContext.SyncSchedules
            .Where(s => s.Id == schedule.Id)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.Name, schedule.Name)
                .SetProperty(s => s.ReportType, schedule.ReportType)
                .SetProperty(s => s.IsEnabled, schedule.IsEnabled)
                .SetProperty(s => s.Frequency, schedule.Frequency)
                .SetProperty(s => s.IntervalMinutes, schedule.IntervalMinutes)
                .SetProperty(s => s.DailyTime, schedule.DailyTime)
                .SetProperty(s => s.TimeZoneId, schedule.TimeZoneId)
                .SetProperty(s => s.LookbackDays, schedule.LookbackDays)
                .SetProperty(s => s.AutoPromote, schedule.AutoPromote)
                .SetProperty(s => s.NextRunAt, schedule.NextRunAt)
                .SetProperty(s => s.LastSuccessfulDataEnd, schedule.LastSuccessfulDataEnd)
                .SetProperty(s => s.UpdatedAt, schedule.UpdatedAt)
                .SetProperty(s => s.UpdatedBy, schedule.UpdatedBy),
                cancellationToken)
            .ConfigureAwait(false);

        return affected == 1;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SyncSchedule>> GetDueAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.SyncSchedules
            .AsNoTracking()
            .Where(s => s.IsEnabled && s.NextRunAt != null && s.NextRunAt <= now)
            .OrderBy(s => s.NextRunAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<bool> TryClaimAsync(int id, DateTimeOffset expectedNextRunAt, DateTimeOffset newNextRunAt, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        // Compare-and-swap on NextRunAt: of several instances racing for the same slot, exactly one
        // UPDATE matches the expected value; the others affect zero rows and skip the run.
        var affected = await dbContext.SyncSchedules
            .Where(s => s.Id == id && s.IsEnabled && s.NextRunAt == expectedNextRunAt)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.NextRunAt, newNextRunAt)
                .SetProperty(s => s.LastRunAt, startedAt),
                cancellationToken)
            .ConfigureAwait(false);

        return affected == 1;
    }

    /// <inheritdoc/>
    public Task RecordSuccessAsync(int id, DateTimeOffset dataEnd, CancellationToken cancellationToken) =>
        dbContext.SyncSchedules
            .Where(s => s.Id == id && (s.LastSuccessfulDataEnd == null || s.LastSuccessfulDataEnd < dataEnd))
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.LastSuccessfulDataEnd, dataEnd), cancellationToken);
}
