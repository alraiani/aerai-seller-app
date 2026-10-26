namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Retry rules shared by every Amazon HTTP client: which connection failures may be retried, and how
/// long to wait between attempts.
/// </summary>
internal static class SpApiRetry
{
    /// <summary>
    /// Whether a request that failed without a response can be sent again without risking a
    /// duplicate on Amazon's side.
    /// </summary>
    /// <param name="exception">The failure from the HTTP stack.</param>
    /// <param name="method">The request's method.</param>
    /// <returns><see langword="true"/> when another attempt is safe.</returns>
    /// <remarks>
    /// DNS, TCP, and TLS failures happen before any byte of the request is sent, so any request can be
    /// retried (typical right after a laptop wakes or the network blips). A connection that drops after
    /// sending may already have reached Amazon: only reads are retried then, never a <c>createReport</c>
    /// POST, because a second report request inside the FBA inventory cooldown makes Amazon fail both.
    /// </remarks>
    public static bool IsTransientConnectionFailure(HttpRequestException exception, HttpMethod method)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(method);

        if (exception.StatusCode is not null)
        {
            return false;
        }

        return exception.HttpRequestError switch
        {
            HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError
                or HttpRequestError.SecureConnectionError or HttpRequestError.ProxyTunnelError => true,
            HttpRequestError.ResponseEnded or HttpRequestError.Unknown => method == HttpMethod.Get || method == HttpMethod.Head,
            _ => false,
        };
    }

    /// <summary>Exponential backoff with full jitter so retrying callers don't hit Amazon in lockstep.</summary>
    /// <param name="attempt">Zero-based attempt that just failed.</param>
    /// <param name="settings">Retry settings.</param>
    /// <returns>The delay before the next attempt.</returns>
    public static TimeSpan Backoff(int attempt, SpApiOptions settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Backoff(attempt, settings.RetryBaseDelay, settings.MaxRetryDelay);
    }

    /// <summary>Exponential backoff with full jitter, for clients with retry settings of their own (Amazon Ads).</summary>
    /// <param name="attempt">Zero-based attempt that just failed.</param>
    /// <param name="baseDelay">First retry delay; doubles each attempt.</param>
    /// <param name="maxDelay">Upper bound for a single delay.</param>
    /// <returns>The delay before the next attempt.</returns>
    public static TimeSpan Backoff(int attempt, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        var exponential = baseDelay.TotalMilliseconds * Math.Pow(2, attempt);
        var capped = Math.Min(exponential, maxDelay.TotalMilliseconds);
#pragma warning disable CA5394 // Jitter is not security-sensitive.
        return TimeSpan.FromMilliseconds(capped / 2 + Random.Shared.NextDouble() * capped / 2);
#pragma warning restore CA5394
    }
}
