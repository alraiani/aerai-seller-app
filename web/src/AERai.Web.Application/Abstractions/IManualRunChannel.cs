using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Hands "Run now" requests from web requests to the background scheduler, so a long SP-API report
/// (which can take many minutes to generate) never ties up an HTTP request.
/// </summary>
public interface IManualRunChannel
{
    /// <summary>Queues a manual run.</summary>
    /// <param name="request">Which schedule, and who asked.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when queued.</returns>
    ValueTask EnqueueAsync(ManualRunRequest request, CancellationToken cancellationToken);

    /// <summary>Waits up to <paramref name="wait"/> for the next queued request.</summary>
    /// <param name="wait">Maximum wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The next request, or <see langword="null"/> if none arrived in time.</returns>
    ValueTask<ManualRunRequest?> DequeueAsync(TimeSpan wait, CancellationToken cancellationToken);
}
