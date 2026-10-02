using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Exchanges the seller's LWA refresh token for short-lived (~1 hour) SP-API access tokens, caching
/// each until shortly before it expires. Singleton, thread-safe.
/// </summary>
/// <param name="httpClientFactory">Factory for the plain (unauthenticated) LWA client.</param>
/// <param name="options">SP-API settings with the LWA credentials.</param>
/// <param name="clock">Clock for expiry checks.</param>
/// <param name="logger">Logger. Token values are never logged.</param>
internal sealed partial class LwaTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<SpApiOptions> options,
    TimeProvider clock,
    ILogger<LwaTokenProvider> logger) : IDisposable
{
    /// <summary>Named HttpClient used for the token endpoint.</summary>
    public const string HttpClientName = "Lwa";

    /// <summary>Refresh this long before expiry so a token never expires mid-request.</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(2);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    /// <summary>Gets a valid access token, refreshing it if needed.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The access token.</returns>
    /// <exception cref="InvalidOperationException">Credentials are missing or LWA rejected them.</exception>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (TryGetCached() is { } cached)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have refreshed while this one waited for the gate.
            if (TryGetCached() is { } refreshed)
            {
                return refreshed;
            }

            var settings = options.Value;
            if (!settings.HasCredentials)
            {
                throw new InvalidOperationException("SP-API credentials (SpApi:ClientId, SpApi:ClientSecret, SpApi:RefreshToken) are not configured.");
            }

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = settings.RefreshToken!,
                ["client_id"] = settings.ClientId!,
                ["client_secret"] = settings.ClientSecret!,
            });

            using var response = await httpClientFactory.CreateClient(HttpClientName)
                .PostAsync(settings.LwaTokenEndpoint, content, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // The body can echo request details; only the status code is logged/surfaced.
                LogRefreshFailed((int)response.StatusCode);
                throw new InvalidOperationException($"Login with Amazon rejected the token refresh (HTTP {(int)response.StatusCode}). Check the SP-API credentials.");
            }

            var token = await response.Content.ReadFromJsonAsync<LwaTokenResponse>(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Login with Amazon returned an empty token response.");

            _accessToken = token.AccessToken;
            _expiresAt = clock.GetUtcNow().AddSeconds(token.ExpiresIn);
            LogRefreshed(token.ExpiresIn);
            return _accessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Discards the cached token (after a 401/403), forcing a refresh on next use.</summary>
    public void Invalidate() => _accessToken = null;

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();

    private string? TryGetCached() =>
        _accessToken is not null && clock.GetUtcNow() < _expiresAt - RefreshMargin ? _accessToken : null;

    private sealed record LwaTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    [LoggerMessage(Level = LogLevel.Information, Message = "Refreshed SP-API access token (expires in {ExpiresInSeconds}s)")]
    private partial void LogRefreshed(int expiresInSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "SP-API token refresh failed with HTTP {StatusCode}")]
    private partial void LogRefreshFailed(int statusCode);
}
