using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.Email;

/// <summary>
/// <see cref="IEmailSender"/> over Resend's HTTP API (<c>POST /emails</c>). Used in Azure, where no
/// SMTP server is available; the API key is the only credential.
/// </summary>
/// <param name="http">Client whose base address is <see cref="EmailOptions.ResendBaseUrl"/>.</param>
/// <param name="options">Email settings.</param>
/// <param name="logger">Logger. Logs subject only — never bodies, which can contain reset links and sign-in codes.</param>
internal sealed partial class ResendEmailSender(HttpClient http, IOptions<EmailOptions> options, ILogger<ResendEmailSender> logger) : IEmailSender
{
    /// <summary>Resend's send-email path, relative to the base address.</summary>
    public const string SendPath = "emails";

    /// <inheritdoc/>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ResendApiKey);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Resend refused the message (bad key, unverified sender, quota).</exception>
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = options.Value;
        if (settings.ResendApiKey is not { Length: > 0 } apiKey)
        {
            LogNotConfigured(message.Subject);
            return;
        }

        var payload = new SendEmailRequest(
            $"{settings.FromName} <{settings.FromAddress}>",
            [string.IsNullOrWhiteSpace(message.ToName) ? message.To : $"{message.ToName} <{message.To}>"],
            message.Subject,
            message.HtmlBody,
            message.TextBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, SendPath) { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // Resend's error body is { name, message, statusCode } and never echoes what we sent, so its
            // message is safe to surface; our own request body (with the link or code) is never included.
            var reason = await ReadErrorMessageAsync(response, cancellationToken).ConfigureAwait(false);
            LogRejected(message.Subject, (int)response.StatusCode, reason);
            throw new InvalidOperationException($"Resend rejected the email ({(int)response.StatusCode}): {reason}");
        }

        LogSent(message.Subject);
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(error?.Message) ? response.ReasonPhrase ?? "no reason given" : error.Message;
        }
        catch (JsonException)
        {
            return response.ReasonPhrase ?? "no reason given";
        }
    }

    private sealed record SendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text);

    private sealed record ErrorResponse([property: JsonPropertyName("message")] string? Message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent email '{Subject}' through Resend")]
    private partial void LogSent(string subject);

    [LoggerMessage(Level = LogLevel.Error, Message = "Resend rejected email '{Subject}' with status {StatusCode}: {Reason}")]
    private partial void LogRejected(string subject, int statusCode, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email is not configured (Email:ResendApiKey); dropped '{Subject}'")]
    private partial void LogNotConfigured(string subject);
}
