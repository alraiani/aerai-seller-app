using System.Collections.Concurrent;

namespace AERai.Web.Application.Ingestion;

/// <summary>In-memory <see cref="IRunStopSignal"/>: the runs this instance is executing right now.</summary>
public sealed class RunStopSignal : IRunStopSignal
{
    private readonly ConcurrentDictionary<long, TrackedRun> _runs = new();

    /// <inheritdoc/>
    public TrackedRun Track(long runId, CancellationToken cancellationToken)
    {
        var tracked = new TrackedRun(runId, Release, cancellationToken);
        _runs[runId] = tracked;
        return tracked;
    }

    /// <inheritdoc/>
    public bool RequestStop(long runId, string stoppedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stoppedBy);

        return _runs.TryGetValue(runId, out var tracked) && tracked.Stop(stoppedBy);
    }

    // Only removes the entry it was given, so a run tracked again (a later check) is never dropped by an older one.
    private void Release(TrackedRun tracked) => _runs.TryRemove(new KeyValuePair<long, TrackedRun>(tracked.RunId, tracked));
}
