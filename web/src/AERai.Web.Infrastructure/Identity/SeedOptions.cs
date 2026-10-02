namespace AERai.Web.Infrastructure.Identity;

/// <summary>
/// Optional first-run administrator account. Bound from the <c>Seed</c> configuration section.
/// </summary>
/// <remarks>
/// Values must come from user-secrets, environment variables, or Key Vault — never appsettings.json.
/// The account is created only when both values are set and no user with that email exists.
/// </remarks>
public sealed class SeedOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Seed";

    /// <summary>Email of the administrator to create.</summary>
    public string? AdminEmail { get; set; }

    /// <summary>Initial password for that administrator.</summary>
    public string? AdminPassword { get; set; }
}
