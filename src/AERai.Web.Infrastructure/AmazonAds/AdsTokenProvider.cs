using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.AmazonAds;

/// <summary>
/// Exchanges the advertising refresh token for short-lived (~1 hour) Ads API access tokens, caching each
/// until shortly before it expires, per region. Singleton, thread-safe.
/// </summary>
/// <remarks>
/// Kept apart from the SP-API token cache: the two APIs are authorized separately (an advertising-scoped
/// grant vs. a selling-partner one), so their tokens are not interchangeable.
/// </remarks>
/// <param name="httpClientFactory">Factory for the plain (unauthenticated) LWA client.</param>
/// <param name="options">Ads settings with the LWA credentials.</param>
/// <param name="clock">Clock for expiry checks.</param>
/// <param name="logger">Logger. Token values are never logged.</param>
internal sealed partial class AdsTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<AmazonAdsOptions> options,
    TimeProvider clock,
    ILogger<AdsTokenProvider> logger) : IDisposable
{
    /// <summary>Named HttpClient used for the token endpoint.</summary>
    public const string HttpClientName = "AdsLwa";

    /// <summary>Refresh this long before expiry so a token never expires mid-request.</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<AmazonRegion, CachedToken> _tokens = new();

    /// <summary>Returns a valid access token for a region, refreshing it when missing or near expiry.</summary>
    /// <param name="region">Region to authorize for.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The access token.</returns>
    /// <exception cref="InvalidOperationException">The credentials are missing or LWA rejected them.</exception>
    public async Task<string> GetAccessTokenAsync(AmazonRegion region, CancellationToken cancellationToken)
    {
        var entry = _tokens.GetOrAdd(region, _ => new CachedToken());
        if (TryGetCached(entry) is { } cached)
        {
            return cached;
        }

        await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have refreshed while this one waited for the gate.
            if (TryGetCached(entry) is { } refreshed)
            {
                return refreshed;
            }

            var settings = options.Value;
            if (!settings.HasCredentialsFor(region))
            {
                throw new InvalidOperationException(
                    "Amazon Ads credentials are not configured (AmazonAds:ClientId, AmazonAds:ClientSecret, AmazonAds:RefreshToken).");
            }

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",

                // HasCredentialsFor above guarantees all three are non-blank.
                ["refresh_token"] = settings.RefreshTokenFor(region)!,
                ["client_id"] = settings.ClientId!,
                ["client_secret"] = settings.ClientSecret!,
            });

            using var response = await httpClientFactory.CreateClient(HttpClientName)
                .PostAsync(settings.LwaTokenEndpoint, content, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // The body can echo request details; only the status code is logged/surfaced.
                LogRefreshFailed(region, (int)response.StatusCode);
                throw new InvalidOperationException($"Login with Amazon rejected the Amazon Ads token refresh (HTTP {(int)response.StatusCode}). Check the AmazonAds credentials.");
            }

            var token = await response.Content.ReadFromJsonAsync<LwaTokenResponse>(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Login with Amazon returned an empty token response.");

            entry.AccessToken = token.AccessToken;
            entry.ExpiresAt = clock.GetUtcNow().AddSeconds(token.ExpiresIn);
            LogRefreshed(region, token.ExpiresIn);
            return token.AccessToken;
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    /// <summary>Drops a region's cached token so the next call refreshes it (after a 401).</summary>
    /// <param name="region">Region.</param>
    public void Invalidate(AmazonRegion region)
    {
        if (_tokens.TryGetValue(region, out var entry))
        {
            entry.AccessToken = null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var entry in _tokens.Values)
        {
            entry.Gate.Dispose();
        }
    }

    private string? TryGetCached(CachedToken entry) =>
        entry.AccessToken is { } token && clock.GetUtcNow() < entry.ExpiresAt - RefreshMargin ? token : null;

    /// <summary>One region's token and the lock that serializes its refreshes.</summary>
    private sealed class CachedToken
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public string? AccessToken { get; set; }

        public DateTimeOffset ExpiresAt { get; set; }
    }

    private sealed record LwaTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    [LoggerMessage(Level = LogLevel.Information, Message = "Refreshed Amazon Ads access token for {Region} (expires in {ExpiresInSeconds}s)")]
    private partial void LogRefreshed(AmazonRegion region, int expiresInSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Amazon Ads token refresh for {Region} failed with HTTP {StatusCode}")]
    private partial void LogRefreshFailed(AmazonRegion region, int statusCode);
}
