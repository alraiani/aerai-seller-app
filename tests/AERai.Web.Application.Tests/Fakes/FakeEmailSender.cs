using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Email;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>Records sent messages instead of delivering them.</summary>
internal sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public bool IsConfigured => true;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
