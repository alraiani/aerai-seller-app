using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.SpApiClient;

/// <summary>
/// Exchanges the refresh_token for a short-lived LWA access_token and caches it
/// in memory, refreshing transparently before expiry or on demand after a 401.
/// SP-API no longer requires AWS SigV4/IAM signing (dropped 2023) — just this token.
/// </summary>
public sealed class LwaTokenProvider(
    IHttpClientFactory httpClientFactory,
    ICredentialStore credentialStore,
    ILogger<LwaTokenProvider> logger)
{
    private const string TokenUrl = "https://api.amazon.com/auth/o2/token";

    // Refresh a little before the token actually expires to avoid racing a 401.
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromMinutes(2);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cachedAccessToken;
    private DateTimeOffset _cachedExpiresAt = DateTimeOffset.MinValue;

    public async Task<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && _cachedAccessToken is not null && DateTimeOffset.UtcNow < _cachedExpiresAt)
        {
            return _cachedAccessToken;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have already refreshed while we waited for the lock.
            if (!forceRefresh && _cachedAccessToken is not null && DateTimeOffset.UtcNow < _cachedExpiresAt)
            {
                return _cachedAccessToken;
            }

            var credentials = await credentialStore.GetCredentialsAsync(cancellationToken)
                ?? throw new InvalidOperationException("No SP-API credentials configured. Set them in Settings first.");

            var client = httpClientFactory.CreateClient(nameof(LwaTokenProvider));
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = credentials.RefreshToken,
                ["client_id"] = credentials.ClientId,
                ["client_secret"] = credentials.ClientSecret,
            };

            using var response = await client.PostAsync(TokenUrl, new FormUrlEncodedContent(form), cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<LwaTokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("LWA token response was empty.");

            _cachedAccessToken = payload.AccessToken;
            _cachedExpiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn) - ExpiryBuffer;

            logger.LogInformation("Refreshed LWA access token, expires at {ExpiresAt}", _cachedExpiresAt);
            return _cachedAccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed record LwaTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
