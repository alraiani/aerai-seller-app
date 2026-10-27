namespace AERai.Web.Application.Ingestion;

/// <summary>A sync run being executed, which a user can stop through <see cref="IRunStopSignal"/>.</summary>
public sealed class TrackedRun : IDisposable
{
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _source;
    private readonly Action<TrackedRun> _release;
    private bool _disposed;

    /// <summary>Creates the tracked run.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="release">Called once when the work ends.</param>
    /// <param name="cancellationToken">The caller's own token (shutdown), linked into <see cref="Token"/>.</param>
    internal TrackedRun(long runId, Action<TrackedRun> release, CancellationToken cancellationToken)
    {
        RunId = runId;
        _source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _release = release;
    }

    /// <summary>The run.</summary>
    public long RunId { get; }

    /// <summary>Cancels when a user stops the run or the application shuts down.</summary>
    public CancellationToken Token => _source.Token;

    /// <summary>Who stopped the run, or <see langword="null"/> while nobody has.</summary>
    public string? StoppedBy { get; private set; }

    /// <summary>Signals the run to stop.</summary>
    /// <param name="stoppedBy">Who stopped it.</param>
    /// <returns>Whether the run was still working (a finished run can't be stopped).</returns>
    internal bool Stop(string stoppedBy)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            StoppedBy ??= stoppedBy;
            _source.Cancel();
            return true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _release(this);
        _source.Dispose();
    }
}
