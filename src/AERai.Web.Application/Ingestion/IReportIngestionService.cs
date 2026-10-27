using AERai.Web.Application.Common;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Runs a schedule end to end: SP-API report → raw blob → staging batch → (optional) promotion.
/// </summary>
public interface IReportIngestionService
{
    /// <summary>
    /// Starts one run of a schedule and records it in the run history. Settlement runs complete here.
    /// Requested reports return still <see cref="Domain.Ingestion.SyncRunStatus.Running"/>, waiting
    /// for Amazon to generate the report; the scheduler finishes them with <see cref="ContinueAsync"/>.
    /// </summary>
    /// <param name="scheduleId">Schedule to run.</param>
    /// <param name="trigger">Whether the scheduler or a user started it.</param>
    /// <param name="triggeredBy">User name, or <c>scheduler</c>.</param>
    /// <param name="backfill">
    /// <see langword="null"/> for a normal run. Otherwise re-pull this window of history (Orders:
    /// data window; Settlements: report creation date). A backfill never moves the schedule's
    /// last-successful marker backwards, so the regular cadence is unaffected.
    /// </param>
    /// <param name="cancellationToken">Cancels the run (e.g. app shutdown).</param>
    /// <returns>The run outcome so far. Failures are recorded and returned, not thrown.</returns>
    /// <exception cref="InvalidOperationException">The schedule does not exist.</exception>
    Task<SyncRunSummary> RunAsync(int scheduleId, Domain.Ingestion.SyncTrigger trigger, string triggeredBy, BackfillWindow? backfill, CancellationToken cancellationToken);

    /// <summary>
    /// Checks once on the Amazon report a run is waiting for. Still generating: schedules the next
    /// check (or fails the run once <see cref="IngestionOptions.ReportMaxWait"/> has passed). Ready:
    /// downloads, lands, stages, optionally promotes, and completes the run.
    /// </summary>
    /// <param name="runId">A run returned still running by <see cref="RunAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the check (e.g. app shutdown); the run then stays waiting.</param>
    /// <returns>The run outcome so far. Failures are recorded and returned, not thrown.</returns>
    /// <exception cref="InvalidOperationException">The run or its schedule does not exist.</exception>
    Task<SyncRunSummary> ContinueAsync(long runId, CancellationToken cancellationToken);

    /// <summary>
    /// Stops a run that hasn't finished. A run waiting on Amazon is ended at once (marked
    /// <see cref="Domain.Ingestion.SyncRunStatus.Cancelled"/>) and Amazon is asked to stop making its report (best
    /// effort: Amazon only cancels one it hasn't started). A run this instance is executing is signalled through
    /// <see cref="IRunStopSignal"/> and ends after the call in progress, keeping anything already saved.
    /// </summary>
    /// <param name="runId">The run.</param>
    /// <param name="marketplaceId">The marketplace in view; another marketplace's run is not found.</param>
    /// <param name="stoppedBy">Who stopped it, recorded on the run.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A message for the user, or why the run could not be stopped.</returns>
    Task<Result<string>> StopAsync(long runId, string marketplaceId, string stoppedBy, CancellationToken cancellationToken);
}
