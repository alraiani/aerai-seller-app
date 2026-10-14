using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Retries connection failures (<see cref="SpApiRetry.IsTransientConnectionFailure"/>) for the Amazon
/// clients outside <see cref="SpApiPipelineHandler"/>: the Login with Amazon token endpoint and the
/// pre-signed report document downloads. HTTP error responses pass through untouched.
/// </summary>
/// <param name="options">Retry settings.</param>
/// <param name="clock">Clock for retry delays.</param>
/// <param name="logger">Logger. URLs are never logged (document URLs are pre-signed).</param>
internal sealed partial class SpApiConnectionRetryHandler(
    IOptions<SpApiOptions> options,
    TimeProvider clock,
    ILogger<SpApiConnectionRetryHandler> logger) : DelegatingHandler
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = options.Value;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt < settings.MaxRetries && SpApiRetry.IsTransientConnectionFailure(ex, request.Method))
            {
                var delay = SpApiRetry.Backoff(attempt, settings);
                LogRetry(request.RequestUri?.Host ?? "unknown", ex.HttpRequestError, attempt + 1, delay.TotalSeconds);
                await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection to {Host} failed ({Error}); retry {Retry} in {DelaySeconds:0.0}s")]
    private partial void LogRetry(string host, HttpRequestError error, int retry, double delaySeconds);
}
