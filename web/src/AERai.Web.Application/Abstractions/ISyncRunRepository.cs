using AERai.Web.Application.Common;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Persistence for <see cref="SyncRun"/> history and the <see cref="IngestedReport"/> ledger.
/// </summary>
public interface ISyncRunRepository
{
    /// <summary>Inserts a run in the <see cref="SyncRunStatus.Running"/> state.</summary>
    /// <param name="run">The new run.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new run id.</returns>
    Task<long> StartAsync(SyncRun run, CancellationToken cancellationToken);

    /// <summary>Saves the final state of a run, clearing any pending Amazon report.</summary>
    /// <param name="run">The run, with status and completion fields set.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when saved.</returns>
    Task CompleteAsync(SyncRun run, CancellationToken cancellationToken);

    /// <summary>Gets one run.</summary>
    /// <param name="id">Run id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The run, or <see langword="null"/> if it does not exist.</returns>
    Task<SyncRun?> GetAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a still-running run's progress: the data window, the Amazon report it is waiting for,
    /// and when to check on it next.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SaveProgressAsync(SyncRun run, CancellationToken cancellationToken);

    /// <summary>Gets runs waiting for an Amazon report whose next check is due.</summary>
    /// <param name="now">Current time.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Due waiting runs, earliest first.</returns>
    Task<IReadOnlyList<SyncRun>> GetPendingDueAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Gets the earliest next check of any run waiting for an Amazon report.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The earliest <see cref="SyncRun.NextCheckAt"/>, or <see langword="null"/> when nothing is waiting.</returns>
    Task<DateTimeOffset?> GetNextPendingCheckAtAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Atomically claims a due check by moving its next check time forward, but only if no one else
    /// has already done so (compare-and-swap, like <see cref="ISyncScheduleRepository.TryClaimAsync"/>).
    /// </summary>
    /// <param name="id">Run id.</param>
    /// <param name="expectedNextCheckAt">The next check time the caller saw.</param>
    /// <param name="newNextCheckAt">When another instance may retry if this one dies mid-check.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> when this caller won the claim.</returns>
    Task<bool> TryClaimCheckAsync(long id, DateTimeOffset expectedNextCheckAt, DateTimeOffset newNextCheckAt, CancellationToken cancellationToken);

    /// <summary>Whether a schedule has a run still waiting for an Amazon report.</summary>
    /// <param name="scheduleId">Schedule id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> when one is waiting.</returns>
    Task<bool> HasPendingAsync(int scheduleId, CancellationToken cancellationToken);

    /// <summary>
    /// Fails runs left running by a process that stopped without finishing them: still running, not
    /// waiting for an Amazon report, and started before <paramref name="startedBefore"/>.
    /// </summary>
    /// <param name="startedBefore">Only runs started before this are considered abandoned.</param>
    /// <param name="message">Failure message to record.</param>
    /// <param name="completedAt">Completion time to record.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many runs were failed.</returns>
    Task<int> FailAbandonedAsync(DateTimeOffset startedBefore, string message, DateTimeOffset completedAt, CancellationToken cancellationToken);

    /// <summary>Lists one marketplace's runs, newest first.</summary>
    /// <param name="marketplaceId">Marketplace whose runs to list.</param>
    /// <param name="scheduleId">Optional filter.</param>
    /// <param name="request">Paging.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One page of runs.</returns>
    Task<PagedResult<SyncRun>> ListAsync(string marketplaceId, int? scheduleId, PageRequest request, CancellationToken cancellationToken);

    /// <summary>Gets the latest run of each schedule.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Latest run keyed by schedule id.</returns>
    Task<IReadOnlyDictionary<int, SyncRun>> GetLatestByScheduleAsync(CancellationToken cancellationToken);

    /// <summary>Whether an Amazon report has already been ingested.</summary>
    /// <param name="amazonReportId">Amazon report id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> if ingested before.</returns>
    Task<bool> IsReportIngestedAsync(string amazonReportId, CancellationToken cancellationToken);

    /// <summary>Adds an entry to the ingested-report ledger.</summary>
    /// <param name="report">The ledger entry.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when saved.</returns>
    Task AddIngestedReportAsync(IngestedReport report, CancellationToken cancellationToken);
}
