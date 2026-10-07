using AERai.Web.Application.Abstractions;

namespace AERai.Web.Infrastructure.Alerts;

/// <summary>
/// In-process <see cref="IStockAlertRefreshSignal"/> consumed by <see cref="StockAlertWorker"/>. A
/// request made on another app instance is not seen here; that instance refreshes itself, and the
/// shared alert tables converge either way.
/// </summary>
internal sealed class StockAlertRefreshSignal : IStockAlertRefreshSignal, IDisposable
{
    // At most one pending request: everything asked for before the worker looks is one refresh.
    private readonly SemaphoreSlim _requested = new(0, 1);

    /// <inheritdoc/>
    public void Request()
    {
        try
        {
            _requested.Release();
        }
        catch (SemaphoreFullException)
        {
            // A refresh is already pending and will include this change.
        }
    }

    /// <summary>Waits until a refresh is requested or <paramref name="timeout"/> elapses.</summary>
    /// <param name="timeout">Longest wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns><see langword="true"/> when a refresh was requested.</returns>
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _requested.WaitAsync(timeout, cancellationToken);

    /// <summary>Clears a pending request (one that arrived while a refresh was already about to run).</summary>
    public void Clear() => _requested.Wait(0, CancellationToken.None);

    /// <inheritdoc/>
    public void Dispose() => _requested.Dispose();
}
