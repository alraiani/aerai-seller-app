using System.Threading.Channels;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;

namespace AERai.Web.Infrastructure.Ingestion;

/// <summary>
/// In-process <see cref="IManualRunChannel"/> over a bounded <see cref="Channel{T}"/> plus a wake
/// signal. Requests live in memory only: a "Run now" queued just before a restart is lost, which is
/// acceptable because the user can simply click it again and scheduled runs are unaffected.
/// </summary>
internal sealed class ManualRunChannel : IManualRunChannel, IDisposable
{
    private readonly Channel<ManualRunRequest> _channel = Channel.CreateBounded<ManualRunRequest>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    // At most one pending wake: several wakes before the scheduler looks are one recompute.
    private readonly SemaphoreSlim _wake = new(0, 1);

    /// <inheritdoc/>
    public ValueTask EnqueueAsync(ManualRunRequest request, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(request, cancellationToken);

    /// <inheritdoc/>
    public void Wake()
    {
        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // A wake is already pending; the scheduler will recompute once for both.
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ManualRunRequest?> DequeueAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        if (_channel.Reader.TryRead(out var ready))
        {
            return ready;
        }

        if (_wake.Wait(0, CancellationToken.None))
        {
            return null;
        }

        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        waiting.CancelAfter(wait);
        var read = _channel.Reader.WaitToReadAsync(waiting.Token).AsTask();
        var woken = _wake.WaitAsync(waiting.Token);
        try
        {
            await Task.WhenAny(read, woken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return _channel.Reader.TryRead(out var request) ? request : null;
        }
        finally
        {
            // Stop whichever wait is still pending. If both finished, a consumed wake is harmless:
            // the scheduler recomputes after every return anyway.
            await waiting.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(read, woken).ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _wake.Dispose();
}
