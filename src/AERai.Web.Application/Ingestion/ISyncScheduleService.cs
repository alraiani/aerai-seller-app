using AERai.Web.Application.Common;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Schedule management use cases behind the Schedules page.
/// </summary>
public interface ISyncScheduleService
{
    /// <summary>Validates and creates a schedule.</summary>
    /// <param name="input">Settings.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new id, or the validation failure.</returns>
    Task<Result<int>> CreateAsync(SyncScheduleInput input, string user, CancellationToken cancellationToken);

    /// <summary>Validates and updates a schedule, recomputing its next run.</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="input">Settings.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or the validation/not-found failure.</returns>
    Task<Result> UpdateAsync(int id, SyncScheduleInput input, string user, CancellationToken cancellationToken);

    /// <summary>Turns automatic runs on or off.</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="enabled">Desired state.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a not-found failure.</returns>
    Task<Result> SetEnabledAsync(int id, bool enabled, string user, CancellationToken cancellationToken);

    /// <summary>Queues an immediate run (processed by the background worker).</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// Whether the report request will wait for Amazon's request limit, or a failure when the schedule
    /// is unknown or Amazon is not connected.
    /// </returns>
    Task<Result<RunNowResult>> RunNowAsync(int id, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Queues a run that re-pulls a window of history (Orders, Settlements). FBA inventory is a
    /// point-in-time snapshot, so it has no history to backfill.
    /// </summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="window">History to re-pull: at most <see cref="SyncScheduleService.MaxLookbackDays"/> days (no limit for the Orders API), not in the future.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a failure for an invalid window, an unsupported report type, or no connection.</returns>
    Task<Result> BackfillAsync(int id, BackfillWindow window, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Queues a normal run (like Run now) of every switched-on schedule in an active marketplace, so one
    /// click refreshes everything. A schedule whose previous run is still waiting on Amazon is skipped.
    /// </summary>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// What was queued, skipped, and will wait for Amazon's request limit, or a failure when Amazon is
    /// not connected or no schedule is switched on.
    /// </returns>
    Task<Result<SyncAllResult>> SyncAllAsync(string user, CancellationToken cancellationToken);

    /// <summary>How many Run now, Sync all, and backfill requests are queued and not started yet.</summary>
    int QueuedRunCount { get; }

    /// <summary>Drops every queued request that hasn't started; runs already started are stopped one by one.</summary>
    /// <param name="user">Who cancelled them, for the log.</param>
    /// <returns>How many were dropped.</returns>
    int CancelQueued(string user);

    /// <summary>
    /// Lists the schedules "backfill all" would run: switched on, in an active marketplace, and pulling
    /// data that has history (inventory snapshots do not).
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The schedules, by marketplace then report type.</returns>
    Task<IReadOnlyList<SyncSchedule>> GetBackfillAllTargetsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Queues a backfill of the same range for every schedule in <see cref="GetBackfillAllTargetsAsync"/>, so
    /// it can be started once and left to finish. Report-based schedules get one run per 30 days; the Orders
    /// API gets one run for the whole range and is queued last, because the scheduler runs one thing at a
    /// time and that run is the slowest. Ranges an earlier backfill already covered are still queued and
    /// counted in the result, so the caller can warn.
    /// </summary>
    /// <param name="window">The range to backfill; the end is clamped to now.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What was queued, or a failure for an invalid range, no connection, nothing to backfill, or too many runs for one go.</returns>
    Task<Result<BackfillAllResult>> BackfillAllAsync(BackfillWindow window, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Finds an earlier finished backfill of the same report type and marketplace that already covers
    /// <paramref name="window"/>, so the caller can warn that queueing it again repeats Amazon calls for
    /// data already loaded. It never blocks anything: a backfill is allowed to repeat.
    /// </summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="window">The range about to be backfilled.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The earlier backfill, or <see langword="null"/> when none covers the range (or the schedule has no history to backfill).</returns>
    Task<BackfillCoverage?> FindCoveringBackfillAsync(int id, BackfillWindow window, CancellationToken cancellationToken);

    /// <summary>
    /// Soft-deletes a schedule. It stops running and disappears from lists, but its run history is
    /// kept and it can be restored.
    /// </summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a not-found failure.</returns>
    Task<Result> DeleteAsync(int id, string user, CancellationToken cancellationToken);

    /// <summary>Restores a deleted schedule. It comes back turned off so nothing runs unexpectedly.</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a failure when not found or its name is now taken.</returns>
    Task<Result> RestoreAsync(int id, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Pauses or resumes all scheduled runs without changing any schedule's own on/off switch.
    /// Manual runs and backfills still work while paused.
    /// </summary>
    /// <param name="paused">Desired state.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success.</returns>
    Task<Result> SetPausedAsync(bool paused, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Applies one action to several schedules. Each schedule is handled independently, so one
    /// failure (e.g. a restore whose name is now taken) doesn't stop the others.
    /// </summary>
    /// <param name="action">What to do.</param>
    /// <param name="ids">Schedule ids (duplicates are ignored).</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many succeeded and the error for each that didn't; a failure when nothing was selected.</returns>
    Task<Result<BulkScheduleResult>> BulkAsync(BulkScheduleAction action, IReadOnlyCollection<int> ids, string user, CancellationToken cancellationToken);

    /// <summary>Builds a schedule from validated input (used for create and to preview next runs).</summary>
    /// <param name="input">Settings.</param>
    /// <returns>The schedule, or the first validation failure.</returns>
    Result<SyncSchedule> Validate(SyncScheduleInput input);
}
