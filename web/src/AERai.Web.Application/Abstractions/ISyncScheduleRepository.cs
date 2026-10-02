using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Persistence for <see cref="SyncSchedule"/>s.
/// </summary>
public interface ISyncScheduleRepository
{
    /// <summary>Lists all schedules ordered by name.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>All schedules (untracked copies).</returns>
    Task<IReadOnlyList<SyncSchedule>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Gets one schedule.</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The schedule (untracked copy), or <see langword="null"/>.</returns>
    Task<SyncSchedule?> GetAsync(int id, CancellationToken cancellationToken);

    /// <summary>Inserts a new schedule.</summary>
    /// <param name="schedule">Schedule with <see cref="SyncSchedule.Id"/> = 0.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new id.</returns>
    Task<int> AddAsync(SyncSchedule schedule, CancellationToken cancellationToken);

    /// <summary>Saves user-editable settings and the recomputed next run time.</summary>
    /// <param name="schedule">The updated schedule.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> when the schedule existed.</returns>
    Task<bool> UpdateSettingsAsync(SyncSchedule schedule, CancellationToken cancellationToken);

    /// <summary>Gets enabled schedules whose next run time has passed.</summary>
    /// <param name="now">Current time.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Due schedules, earliest first.</returns>
    Task<IReadOnlyList<SyncSchedule>> GetDueAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically claims a due schedule by moving its next run time forward, but only if no one else
    /// has already done so. Guarantees a schedule runs once per slot even with several app instances.
    /// </summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="expectedNextRunAt">The next run time the caller saw.</param>
    /// <param name="newNextRunAt">The following slot.</param>
    /// <param name="startedAt">Recorded as the last run time.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> when this caller won the claim.</returns>
    Task<bool> TryClaimAsync(int id, DateTimeOffset expectedNextRunAt, DateTimeOffset newNextRunAt, DateTimeOffset startedAt, CancellationToken cancellationToken);

    /// <summary>Records the end of the data window covered by a successful run.</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="dataEnd">End of the covered window.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when saved.</returns>
    Task RecordSuccessAsync(int id, DateTimeOffset dataEnd, CancellationToken cancellationToken);
}
