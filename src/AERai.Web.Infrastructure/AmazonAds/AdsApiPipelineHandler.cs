using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.SpApi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.AmazonAds;

/// <summary>
/// The single choke point for every Amazon Ads API call: the bearer token, client id, and profile scope
/// headers; retry with backoff on 429/5xx (honoring <c>Retry-After</c>) and on connection failures that are
/// safe to repeat (<see cref="SpApiRetry.IsTransientConnectionFailure"/>); one token refresh on 401; and
/// structured logging of operation, status, attempt, and latency (never tokens).
/// </summary>
/// <remarks>
/// Amazon Ads throttles dynamically and says so with 429 and <c>Retry-After</c> rather than publishing
/// per-operation rates, so unlike SP-API there are no token buckets: the few calls a sync makes back off
/// when told to.
/// </remarks>
/// <param name="tokens">Access token cache.</param>
/// <param name="options">Client id and retry settings.</param>
/// <param name="clock">Clock for retry delays.</param>
/// <param name="logger">Logger.</param>
internal sealed partial class AdsApiPipelineHandler(
    AdsTokenProvider tokens,
    IOptions<AmazonAdsOptions> options,
    TimeProvider clock,
    ILogger<AdsApiPipelineHandler> logger) : DelegatingHandler
{
    private const string ClientIdHeader = "Amazon-Advertising-API-ClientId";
    private const string ScopeHeader = "Amazon-Advertising-API-Scope";

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operation = request.Options.TryGetValue(AdsApiOperation.OptionKey, out var name) ? name : "unknown";
        var region = request.Options.TryGetValue(AdsApiOperation.RegionKey, out var tagged) ? tagged : AmazonRegion.NorthAmerica;
        var settings = options.Value;
        var tokenRefreshed = false;

        request.Headers.Remove(ClientIdHeader);
        request.Headers.Add(ClientIdHeader, settings.ClientId);
        request.Headers.Remove(ScopeHeader);
        if (request.Options.TryGetValue(AdsApiOperation.ProfileKey, out var profileId))
        {
            request.Headers.Add(ScopeHeader, profileId);
        }

        for (var attempt = 0; ; attempt++)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", await tokens.GetAccessTokenAsync(region, cancellationToken).ConfigureAwait(false));

            var started = Stopwatch.GetTimestamp();
            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt < settings.MaxRetries && SpApiRetry.IsTransientConnectionFailure(ex, request.Method))
            {
                var backoff = SpApiRetry.Backoff(attempt, settings.RetryBaseDelay, settings.MaxRetryDelay);
                LogConnectionRetry(operation, region, ex.HttpRequestError, attempt + 1, backoff.TotalSeconds);
                await Task.Delay(backoff, clock, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            LogCall(operation, region, (int)response.StatusCode, attempt, elapsedMs);

            if (response.StatusCode == HttpStatusCode.Unauthorized && !tokenRefreshed)
            {
                // A token can be revoked before its expiry; refresh once, then let a second 401 surface.
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

            var delay = response.Headers.RetryAfter?.Delta is { } retryAfter && retryAfter < settings.MaxRetryDelay
                ? retryAfter
                : SpApiRetry.Backoff(attempt, settings.RetryBaseDelay, settings.MaxRetryDelay);
            response.Dispose();
            LogRetry(operation, attempt + 1, delay.TotalSeconds);
            await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Amazon Ads {Operation} ({Region}) -> {StatusCode} (attempt {Attempt}, {ElapsedMs:0} ms)")]
    private partial void LogCall(string operation, AmazonRegion region, int statusCode, int attempt, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Amazon Ads {Operation} throttled or failed; retry {Retry} in {DelaySeconds:0.0}s")]
    private partial void LogRetry(string operation, int retry, double delaySeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Amazon Ads {Operation} ({Region}) connection failed ({Error}); retry {Retry} in {DelaySeconds:0.0}s")]
    private partial void LogConnectionRetry(string operation, AmazonRegion region, HttpRequestError error, int retry, double delaySeconds);
}
