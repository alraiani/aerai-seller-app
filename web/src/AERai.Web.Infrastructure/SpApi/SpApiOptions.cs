using System.ComponentModel.DataAnnotations;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// SP-API connection settings. Bound from the <c>SpApi</c> configuration section.
/// </summary>
/// <remarks>
/// <see cref="ClientId"/>, <see cref="ClientSecret"/>, and <see cref="RefreshToken"/> are secrets:
/// supply them via user-secrets locally and Key Vault in Azure (<c>SpApi--ClientSecret</c>, etc.).
/// They are never logged or shown in the UI.
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

    /// <summary>Marketplace to report on (Amazon.com by default).</summary>
    [Required]
    public string MarketplaceId { get; set; } = "ATVPDKIKX0DER";

    /// <summary>LWA application client id.</summary>
    public string? ClientId { get; set; }

    /// <summary>LWA application client secret.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Seller authorization refresh token.</summary>
    public string? RefreshToken { get; set; }

    /// <summary>Retries for throttled (429) or failed (5xx) calls before giving up.</summary>
    [Range(0, 10)]
    public int MaxRetries { get; set; } = 5;

    /// <summary>First retry delay; doubles each attempt (plus jitter), capped at <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Upper bound for a single retry delay.</summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Whether all three LWA credentials are present.</summary>
    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(RefreshToken);
}
