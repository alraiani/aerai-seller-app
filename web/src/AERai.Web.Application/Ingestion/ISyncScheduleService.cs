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
    /// <returns>Success, or a failure when the schedule is unknown or Amazon is not connected.</returns>
    Task<Result> RunNowAsync(int id, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Queues a run that re-pulls a window of history (Orders, Settlements). FBA inventory is a
    /// point-in-time snapshot, so it has no history to backfill.
    /// </summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="window">History to re-pull: at most <see cref="SyncScheduleService.MaxLookbackDays"/> days, not in the future.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a failure for an invalid window, an unsupported report type, or no connection.</returns>
    Task<Result> BackfillAsync(int id, BackfillWindow window, string user, CancellationToken cancellationToken);

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
