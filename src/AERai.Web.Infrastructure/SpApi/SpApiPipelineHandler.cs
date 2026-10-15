using System.Diagnostics;
using System.Net;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// The single choke point for every SP-API call: per-operation rate limiting, the LWA access token
/// header, retry with exponential backoff and jitter on 429/5xx and on connection failures that are
/// safe to repeat (<see cref="SpApiRetry.IsTransientConnectionFailure"/>), one token refresh on
/// 401/403, and structured logging of operation, status, attempt, and latency (never tokens).
/// </summary>
/// <param name="tokens">Access token cache.</param>
/// <param name="limiter">Per-operation token buckets.</param>
/// <param name="options">Retry settings.</param>
/// <param name="clock">Clock for retry delays.</param>
/// <param name="logger">Logger.</param>
internal sealed partial class SpApiPipelineHandler(
    LwaTokenProvider tokens,
    SpApiRateLimiter limiter,
    IOptions<SpApiOptions> options,
    TimeProvider clock,
    ILogger<SpApiPipelineHandler> logger) : DelegatingHandler
{
    private const string AccessTokenHeader = "x-amz-access-token";

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operation = request.Options.TryGetValue(SpApiOperation.OptionKey, out var name) ? name : "unknown";

        // Typed clients always tag the region; untagged requests are North America, the original default.
        var region = request.Options.TryGetValue(SpApiOperation.RegionKey, out var tagged) ? tagged : AmazonRegion.NorthAmerica;
        var settings = options.Value;
        var tokenRefreshed = false;

        for (var attempt = 0; ; attempt++)
        {
            using (var lease = await limiter.AcquireAsync(region, operation, cancellationToken).ConfigureAwait(false))
            {
                if (!lease.IsAcquired)
                {
                    throw new InvalidOperationException($"SP-API rate limiter queue is full for {operation}.");
                }
            }

            request.Headers.Remove(AccessTokenHeader);
            request.Headers.Add(AccessTokenHeader, await tokens.GetAccessTokenAsync(region, cancellationToken).ConfigureAwait(false));

            var started = Stopwatch.GetTimestamp();
            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt < settings.MaxRetries && SpApiRetry.IsTransientConnectionFailure(ex, request.Method))
            {
                var backoff = SpApiRetry.Backoff(attempt, settings);
                LogConnectionRetry(operation, region, ex.HttpRequestError, attempt + 1, backoff.TotalSeconds);
                await Task.Delay(backoff, clock, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            LogCall(operation, region, (int)response.StatusCode, attempt, elapsedMs);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden && !tokenRefreshed)
            {
                // A token can be revoked before its expiry; refresh once, then let a second 401/403 surface.
                tokenRefreshed = true;
                tokens.Invalidate(region);
                response.Dispose();
                continue;
            }

            var retryable = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            if (!retryable || attempt >= settings.MaxRetries)
            {
                return response;
            }

            var delay = RetryDelay(response, attempt, settings);
            response.Dispose();
            LogRetry(operation, attempt + 1, delay.TotalSeconds);
            await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Honors <c>Retry-After</c> when Amazon sends it; otherwise <see cref="SpApiRetry.Backoff"/>.
    /// </summary>
    private static TimeSpan RetryDelay(HttpResponseMessage response, int attempt, SpApiOptions settings)
    {
        if (response.Headers.RetryAfter?.Delta is { } retryAfter)
        {
            return retryAfter < settings.MaxRetryDelay ? retryAfter : settings.MaxRetryDelay;
        }

        return SpApiRetry.Backoff(attempt, settings);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SP-API {Operation} ({Region}) -> {StatusCode} (attempt {Attempt}, {ElapsedMs:0} ms)")]
    private partial void LogCall(string operation, AmazonRegion region, int statusCode, int attempt, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SP-API {Operation} throttled or failed; retry {Retry} in {DelaySeconds:0.0}s")]
    private partial void LogRetry(string operation, int retry, double delaySeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SP-API {Operation} ({Region}) connection failed ({Error}); retry {Retry} in {DelaySeconds:0.0}s")]
    private partial void LogConnectionRetry(string operation, AmazonRegion region, HttpRequestError error, int retry, double delaySeconds);
}
