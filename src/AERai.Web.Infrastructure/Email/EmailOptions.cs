using System.ComponentModel.DataAnnotations;

namespace AERai.Web.Infrastructure.Email;

/// <summary>
/// Outgoing email settings. Bound from the <c>Email</c> configuration section.
/// </summary>
/// <remarks>
/// Two providers: <c>Smtp</c> (default) and <c>Resend</c>. Locally SMTP points at the Mailpit
/// container (no credentials). Production uses Resend's HTTP API with <see cref="ResendApiKey"/>
/// stored in Key Vault as <c>Email--ResendApiKey</c>, never in appsettings; the sender's domain must
/// be verified in Resend. Leaving the provider's setting empty (<see cref="Host"/> for SMTP,
/// <see cref="ResendApiKey"/> for Resend) disables email (sends are dropped with a warning).
/// </remarks>
public sealed class EmailOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Email";

    /// <summary>Provider name for <see cref="Provider"/>: Resend's HTTP API.</summary>
    public const string ResendProvider = "Resend";

    /// <summary>Provider name for <see cref="Provider"/>: SMTP via MailKit.</summary>
    public const string SmtpProvider = "Smtp";

    /// <summary>Which provider sends mail: <c>Smtp</c> (default) or <c>Resend</c>.</summary>
    [RegularExpression("^(Smtp|Resend)$", ErrorMessage = "Email:Provider must be Smtp or Resend.")]
    public string Provider { get; set; } = SmtpProvider;

    /// <summary>Resend API key (secret: user-secrets locally, Key Vault <c>Email--ResendApiKey</c> in Azure); empty disables Resend sends.</summary>
    public string? ResendApiKey { get; set; }

    /// <summary>Resend API base address; only changed by tests.</summary>
    [Required]
    public Uri ResendBaseUrl { get; set; } = new("https://api.resend.com/");

    /// <summary>SMTP server host name; empty disables SMTP sends.</summary>
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
