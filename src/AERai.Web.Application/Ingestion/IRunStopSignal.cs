namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Lets a user stop a sync run while this app instance is executing it (paging through an Amazon API, downloading,
/// or staging a report): the run's work observes the token of its <see cref="TrackedRun"/>, and
/// <see cref="RequestStop"/> cancels it.
/// </summary>
/// <remarks>
/// A run waiting on Amazon is not executing anywhere, so it is stopped through the database instead
/// (<see cref="IReportIngestionService.StopAsync"/>); this signal only covers the moments a run is actually working.
/// </remarks>
public interface IRunStopSignal
{
    /// <summary>Starts tracking a run the caller is about to execute; dispose the result when the work ends.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">The caller's own token (application shutdown), linked into the run's token.</param>
    /// <returns>The tracked run, whose token cancels on either a stop or shutdown.</returns>
    TrackedRun Track(long runId, CancellationToken cancellationToken);

    /// <summary>Stops a run this instance is executing.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="stoppedBy">Who stopped it, recorded on the run.</param>
    /// <returns>Whether this instance was executing it (and has now signalled it to stop).</returns>
    bool RequestStop(long runId, string stoppedBy);
}
