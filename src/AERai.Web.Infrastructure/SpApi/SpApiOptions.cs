using System.ComponentModel.DataAnnotations;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// SP-API connection settings. Bound from the <c>SpApi</c> configuration section.
/// </summary>
/// <remarks>
/// <see cref="ClientId"/>, <see cref="ClientSecret"/>, and <see cref="RefreshToken"/> are secrets:
/// supply them via user-secrets locally and Key Vault in Azure (<c>SpApi--ClientSecret</c>, etc.).
/// They are never logged or shown in the UI. The flat <see cref="Endpoint"/> and
/// <see cref="RefreshToken"/> are the North America region (US, Canada); other regions have their
/// own section (<see cref="Europe"/>), because each region needs its own seller authorization.
/// </remarks>
public sealed class SpApiOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "SpApi";

    /// <summary>Connection mode.</summary>
    public SpApiMode Mode { get; set; } = SpApiMode.Disabled;

    /// <summary>Regional SP-API endpoint (North America by default).</summary>
    [Required]
    public Uri Endpoint { get; set; } = new("https://sellingpartnerapi-na.amazon.com");

    /// <summary>Login with Amazon token endpoint.</summary>
    [Required]
    public Uri LwaTokenEndpoint { get; set; } = new("https://api.amazon.com/auth/o2/token");

    /// <summary>LWA application client id.</summary>
    public string? ClientId { get; set; }

    /// <summary>LWA application client secret.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Seller authorization refresh token for North America.</summary>
    public string? RefreshToken { get; set; }

    /// <summary>Europe region (United Kingdom): endpoint and its own refresh token.</summary>
    public SpApiRegionOptions Europe { get; set; } = new();

    /// <summary>Retries for throttled (429) or failed (5xx) calls before giving up.</summary>
    [Range(0, 10)]
    public int MaxRetries { get; set; } = 5;

    /// <summary>First retry delay; doubles each attempt (plus jitter), capped at <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Upper bound for a single retry delay.</summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Whether all three LWA credentials are present for North America.</summary>
    public bool HasCredentials => HasCredentialsFor(AmazonRegion.NorthAmerica);

    /// <summary>The SP-API endpoint that serves a region.</summary>
    /// <param name="region">SP-API region.</param>
    /// <returns>The endpoint.</returns>
    public Uri EndpointFor(AmazonRegion region) => region switch
    {
        AmazonRegion.NorthAmerica => Endpoint,
        AmazonRegion.Europe => Europe.Endpoint,
        _ => throw new ArgumentOutOfRangeException(nameof(region), region, "Unsupported SP-API region."),
    };

    /// <summary>The seller authorization for a region, if configured.</summary>
    /// <param name="region">SP-API region.</param>
    /// <returns>The refresh token, or <see langword="null"/>.</returns>
    public string? RefreshTokenFor(AmazonRegion region) => region switch
    {
        AmazonRegion.NorthAmerica => RefreshToken,
        AmazonRegion.Europe => Europe.RefreshToken,
        _ => null,
    };

    /// <summary>Whether a region can be called: the shared LWA app plus that region's refresh token.</summary>
    /// <param name="region">SP-API region.</param>
    /// <returns><see langword="true"/> when all three credentials are present.</returns>
    public bool HasCredentialsFor(AmazonRegion region) =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(RefreshTokenFor(region));

    /// <summary>The configuration key a region's refresh token is read from, for error messages.</summary>
    /// <param name="region">SP-API region.</param>
    /// <returns>e.g. <c>SpApi:Europe:RefreshToken</c>.</returns>
    public static string RefreshTokenKey(AmazonRegion region) =>
        region == AmazonRegion.Europe ? $"{SectionName}:{nameof(Europe)}:{nameof(SpApiRegionOptions.RefreshToken)}" : $"{SectionName}:{nameof(RefreshToken)}";
}
