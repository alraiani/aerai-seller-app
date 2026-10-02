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

    /// <summary>Saves the final state of a run.</summary>
    /// <param name="run">The run, with status and completion fields set.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when saved.</returns>
    Task CompleteAsync(SyncRun run, CancellationToken cancellationToken);

    /// <summary>Lists runs, newest first.</summary>
    /// <param name="scheduleId">Optional filter.</param>
    /// <param name="request">Paging.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One page of runs.</returns>
    Task<PagedResult<SyncRun>> ListAsync(int? scheduleId, PageRequest request, CancellationToken cancellationToken);

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
