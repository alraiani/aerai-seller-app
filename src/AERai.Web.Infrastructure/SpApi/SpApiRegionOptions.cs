using System.ComponentModel.DataAnnotations;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Settings for an additional SP-API region. Each region has its own endpoint and its own seller
/// authorization (refresh token); the LWA application (client id/secret) is shared.
/// </summary>
public sealed class SpApiRegionOptions
{
    /// <summary>Regional SP-API endpoint.</summary>
    [Required]
    public Uri Endpoint { get; set; } = new("https://sellingpartnerapi-eu.amazon.com");

    /// <summary>
    /// Seller authorization refresh token for this region. A secret: Key Vault
    /// (<c>SpApi--Europe--RefreshToken</c>) or user-secrets only.
    /// </summary>
    public string? RefreshToken { get; set; }
}
