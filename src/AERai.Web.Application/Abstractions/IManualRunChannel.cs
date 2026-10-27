using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// The background scheduler's inbox. Hands "Run now" requests from web requests to the scheduler,
/// so a long SP-API report never ties up an HTTP request, and wakes the scheduler when schedules
/// change so it can recompute when to run next.
/// </summary>
/// <remarks>
/// The scheduler sleeps until its next known event instead of polling the database, which lets the
/// serverless Azure SQL database auto-pause while nothing is due. Anything that changes when the next
/// run should happen must therefore call <see cref="Wake"/>.
/// </remarks>
public interface IManualRunChannel
{
    /// <summary>Queues a manual run.</summary>
    /// <param name="request">Which schedule, and who asked.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when queued.</returns>
    ValueTask EnqueueAsync(ManualRunRequest request, CancellationToken cancellationToken);

    /// <summary>How many requests are queued and not started yet.</summary>
    int QueuedCount { get; }

    /// <summary>Drops every queued request that hasn't started (a run already started is stopped separately).</summary>
    /// <returns>How many were dropped.</returns>
    int CancelQueued();

    /// <summary>Wakes the scheduler so it re-reads schedules (after a create, edit, enable, pause, ...).</summary>
    void Wake();

    /// <summary>Waits up to <paramref name="wait"/> for the next queued request or a <see cref="Wake"/>.</summary>
    /// <param name="wait">Maximum wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The next request, or <see langword="null"/> if the wait elapsed or the scheduler was woken.</returns>
    ValueTask<ManualRunRequest?> DequeueAsync(TimeSpan wait, CancellationToken cancellationToken);
}
