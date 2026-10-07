using AERai.Web.Application.Email;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Sends transactional email (e.g. password reset links). Implemented over SMTP in Infrastructure:
/// Mailpit locally, a provider such as Azure Communication Services in production.
/// </summary>
public interface IEmailSender
{
    /// <summary>Whether a mail server is configured. When <see langword="false"/>, sends are dropped with a warning.</summary>
    bool IsConfigured { get; }

    /// <summary>Sends a message.</summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the server has accepted the message.</returns>
    /// <remarks>Implementations must never log message bodies: they can contain credentials such as reset links.</remarks>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
