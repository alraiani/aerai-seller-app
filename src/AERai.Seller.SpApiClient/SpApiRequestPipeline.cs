using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.SpApiClient;

/// <summary>
/// The single choke point every SP-API call goes through. No code outside this class
/// (or the typed clients that call it) is allowed to talk to Amazon directly.
/// Handles token attachment, per-operation rate limiting, and throttling/retry.
/// </summary>
public sealed class SpApiRequestPipeline(
    IHttpClientFactory httpClientFactory,
    LwaTokenProvider tokenProvider,
    ICredentialStore credentialStore,
    SpApiOperationRateLimiter rateLimiter,
    ILogger<SpApiRequestPipeline> logger)
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan BaseBackoff = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    /// <param name="operationName">Stable operation identifier (e.g. "Reports.CreateReport") used for rate limiting and logging.</param>
    /// <param name="requestFactory">Builds the request given the configured SP-API host; a new instance is required per attempt.</param>
    public async Task<HttpResponseMessage> SendAsync(
        string operationName,
        Func<string, HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        var credentials = await credentialStore.GetCredentialsAsync(cancellationToken)
            ?? throw new InvalidOperationException("No SP-API credentials configured. Set them in Settings first.");

        for (var attempt = 1; ; attempt++)
        {
            await rateLimiter.WaitAsync(operationName, cancellationToken);

            var accessToken = await tokenProvider.GetAccessTokenAsync(forceRefresh: false, cancellationToken);
            using var request = requestFactory(credentials.ApiHost);
            request.Headers.Add("x-amz-access-token", accessToken);
            logger.LogInformation("SP-API {Operation} request URI: {RequestUri}", operationName, request.RequestUri);

            var client = httpClientFactory.CreateClient(nameof(SpApiRequestPipeline));
            var stopwatch = Stopwatch.StartNew();
            var response = await client.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            logger.LogInformation(
                "SP-API {Operation} -> {StatusCode} in {ElapsedMs}ms (attempt {Attempt})",
                operationName, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, attempt);

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 1)
            {
                response.Dispose();
                await tokenProvider.GetAccessTokenAsync(forceRefresh: true, cancellationToken);
                continue;
            }

            var statusCode = response.StatusCode;
            var isRetryable = statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

            if (!isRetryable || attempt >= MaxAttempts)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                response.Dispose();
                logger.LogError(
                    "SP-API {Operation} failed with {StatusCode}: {ResponseBody}", operationName, (int)statusCode, body);
                throw new SpApiException(operationName, statusCode, body);
            }

            var delay = ComputeBackoffDelay(attempt, response);
            response.Dispose();

            logger.LogWarning(
                "SP-API {Operation} returned {StatusCode}, retrying in {DelayMs}ms (attempt {Attempt}/{MaxAttempts})",
                operationName, (int)statusCode, delay.TotalMilliseconds, attempt, MaxAttempts);

            await Task.Delay(delay, cancellationToken);
        }
    }

    private static TimeSpan ComputeBackoffDelay(int attempt, HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } retryAfter)
        {
            return retryAfter < MaxBackoff ? retryAfter : MaxBackoff;
        }

        var exponential = TimeSpan.FromMilliseconds(BaseBackoff.TotalMilliseconds * Math.Pow(2, attempt - 1));
        var bounded = exponential < MaxBackoff ? exponential : MaxBackoff;
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
        return bounded + jitter;
    }
}
