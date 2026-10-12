using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Email;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>Records sent messages instead of delivering them.</summary>
internal sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public bool IsConfigured => true;

    /// <summary>When set, every send fails like a rejected provider call.</summary>
    public bool Fail { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new InvalidOperationException("Provider rejected the email.");
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }
}
