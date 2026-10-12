using System.ComponentModel.DataAnnotations;

namespace AERai.Web.Infrastructure.Email;

/// <summary>
/// Outgoing email settings. Bound from the <c>Email</c> configuration section.
/// </summary>
/// <remarks>
/// Locally this points at the Mailpit container (no credentials). In production use any SMTP
/// provider — e.g. Azure Communication Services (smtp.azurecomm.net:587) or SendGrid — with
/// <see cref="Password"/> stored in Key Vault as <c>Email--Password</c>, never in appsettings.
/// Leaving <see cref="Host"/> empty disables email (sends are dropped with a warning).
/// </remarks>
public sealed class EmailOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Email";

    /// <summary>SMTP server host name; empty disables email.</summary>
    public string? Host { get; set; }

    /// <summary>SMTP port (587 for STARTTLS submission, 465 for implicit TLS, 1025 for Mailpit).</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    /// <summary>
    /// Transport security: <c>StartTls</c> (default; required unless the server supports it),
    /// <c>SslOnConnect</c> (port 465), or <c>None</c> (local Mailpit only).
    /// </summary>
    public string Security { get; set; } = "StartTls";

    /// <summary>SMTP user name, if the server requires authentication.</summary>
    public string? UserName { get; set; }

    /// <summary>SMTP password (secret: user-secrets locally, Key Vault in Azure).</summary>
    public string? Password { get; set; }

    /// <summary>Sender address, e.g. <c>no-reply@aeraigroup.com</c>.</summary>
    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = "no-reply@aeraigroup.com";

    /// <summary>Sender display name.</summary>
    [Required]
    public string FromName { get; set; } = "AERai Seller";
}
