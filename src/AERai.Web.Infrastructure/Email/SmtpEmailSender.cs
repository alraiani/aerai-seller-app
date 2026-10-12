using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AERai.Web.Infrastructure.Email;

/// <summary>
/// <see cref="IEmailSender"/> over SMTP using MailKit. Opens one connection per message, which is
/// plenty for transactional volume (password resets) and avoids holding idle connections.
/// </summary>
/// <param name="options">Email settings.</param>
/// <param name="logger">Logger. Logs recipient and subject only — never bodies, which can contain reset links.</param>
internal sealed partial class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    /// <inheritdoc/>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.Host);

    /// <inheritdoc/>
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = options.Value;
        if (settings.Host is not { Length: > 0 } host)
        {
            LogNotConfigured(message.Subject);
            return;
        }

        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName, message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        var security = settings.Security switch
        {
            "None" => SecureSocketOptions.None,
            "SslOnConnect" => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.StartTls,
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, settings.Port, security, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(settings.UserName))
        {
            await client.AuthenticateAsync(settings.UserName, settings.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }

        await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
        LogSent(message.Subject);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent email '{Subject}'")]
    private partial void LogSent(string subject);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email is not configured (Email:Host); dropped '{Subject}'")]
    private partial void LogNotConfigured(string subject);
}
