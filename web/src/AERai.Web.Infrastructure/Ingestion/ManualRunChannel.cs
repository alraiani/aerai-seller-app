using System.Threading.Channels;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;

namespace AERai.Web.Infrastructure.Ingestion;

/// <summary>
/// In-process <see cref="IManualRunChannel"/> over a bounded <see cref="Channel{T}"/>. Requests live
/// in memory only: a "Run now" queued just before a restart is lost, which is acceptable because the
/// user can simply click it again and scheduled runs are unaffected.
/// </summary>
internal sealed class ManualRunChannel : IManualRunChannel
{
    private readonly Channel<ManualRunRequest> _channel = Channel.CreateBounded<ManualRunRequest>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    /// <inheritdoc/>
    public ValueTask EnqueueAsync(ManualRunRequest request, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(request, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ManualRunRequest?> DequeueAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        if (_channel.Reader.TryRead(out var ready))
        {
            return ready;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(wait);
        try
        {
            return await _channel.Reader.WaitToReadAsync(timeout.Token).ConfigureAwait(false) && _channel.Reader.TryRead(out var request)
                ? request
                : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null; // The wait elapsed without a request.
        }
    }
}
