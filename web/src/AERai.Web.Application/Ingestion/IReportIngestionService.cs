namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Runs a schedule end to end: SP-API report → raw blob → staging batch → (optional) promotion.
/// </summary>
public interface IReportIngestionService
{
    /// <summary>Executes one run of a schedule and records it in the run history.</summary>
    /// <param name="scheduleId">Schedule to run.</param>
    /// <param name="trigger">Whether the scheduler or a user started it.</param>
    /// <param name="triggeredBy">User name, or <c>scheduler</c>.</param>
    /// <param name="cancellationToken">Cancels the run (e.g. app shutdown).</param>
    /// <returns>The run outcome. Failures are recorded and returned, not thrown.</returns>
    /// <exception cref="InvalidOperationException">The schedule does not exist.</exception>
    Task<SyncRunSummary> RunAsync(int scheduleId, Domain.Ingestion.SyncTrigger trigger, string triggeredBy, CancellationToken cancellationToken);
}
