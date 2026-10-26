using System.ComponentModel.DataAnnotations;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.AmazonAds;

/// <summary>
/// Amazon Ads API connection settings. Bound from the <c>AmazonAds</c> configuration section.
/// </summary>
/// <remarks>
/// The Ads API is not SP-API: it has its own Login with Amazon authorization (an advertising-scoped refresh
/// token, usually issued to the same LWA application), its own regional hosts, and every call is scoped to an
/// advertising profile. <see cref="ClientId"/>, <see cref="ClientSecret"/>, and the refresh tokens are secrets:
/// user-secrets locally, Key Vault in Azure (<c>AmazonAds--RefreshToken</c>, etc.). They are never logged.
/// </remarks>
public sealed class AmazonAdsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "AmazonAds";

    /// <summary>Connection mode.</summary>
    public AmazonAdsMode Mode { get; set; } = AmazonAdsMode.Disabled;

    /// <summary>North America Ads API host (US, Canada).</summary>
    [Required]
    public Uri Endpoint { get; set; } = new("https://advertising-api.amazon.com");

    /// <summary>Europe Ads API host (United Kingdom).</summary>
    [Required]
    public Uri EuropeEndpoint { get; set; } = new("https://advertising-api-eu.amazon.com");

    /// <summary>Login with Amazon token endpoint.</summary>
    [Required]
    public Uri LwaTokenEndpoint { get; set; } = new("https://api.amazon.com/auth/o2/token");

    /// <summary>LWA application client id; also sent on every call as <c>Amazon-Advertising-API-ClientId</c>.</summary>
    public string? ClientId { get; set; }

    /// <summary>LWA application client secret.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Advertising authorization refresh token (North America, and Europe unless it has its own).</summary>
    public string? RefreshToken { get; set; }

    /// <summary>Optional Europe refresh token, for when the European advertising account was authorized separately.</summary>
    public string? EuropeRefreshToken { get; set; }

    /// <summary>Retries for throttled (429) or failed (5xx) calls, and for connection failures, before giving up.</summary>
    [Range(0, 10)]
    public int MaxRetries { get; set; } = 5;

    /// <summary>First retry delay; doubles each attempt (plus jitter), capped at <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Upper bound for a single retry delay.</summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>The Ads API host that serves a region.</summary>
    /// <param name="region">Amazon region.</param>
    /// <returns>The host.</returns>
    public Uri EndpointFor(AmazonRegion region) => region switch
    {
        AmazonRegion.NorthAmerica => Endpoint,
        AmazonRegion.Europe => EuropeEndpoint,
        _ => throw new ArgumentOutOfRangeException(nameof(region), region, "Unsupported Amazon Ads region."),
    };

    /// <summary>The advertising authorization for a region: its own when given, else the main one.</summary>
    /// <param name="region">Amazon region.</param>
    /// <returns>The refresh token, or <see langword="null"/>.</returns>
    public string? RefreshTokenFor(AmazonRegion region) =>
        region == AmazonRegion.Europe && !string.IsNullOrWhiteSpace(EuropeRefreshToken) ? EuropeRefreshToken : RefreshToken;

    /// <summary>Whether a region can be called: client id, secret, and a refresh token.</summary>
    /// <param name="region">Amazon region.</param>
    /// <returns><see langword="true"/> when all three are present.</returns>
    public bool HasCredentialsFor(AmazonRegion region) =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(RefreshTokenFor(region));
}
