using System.Net;
using System.Text.Json;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Email;
using AERai.Web.Infrastructure.Email;
using AERai.Web.Infrastructure.Tests.SpApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>Resend sender against a stubbed HTTP handler (no network, no API key needed).</summary>
public sealed class ResendEmailSenderTests : IDisposable
{
    private static readonly EmailMessage Message = new("someone@aeraigroup.com", "Some One", "Subject line", "<p>Code 123456</p>", "Code 123456");

    private readonly StubHttpHandler _handler = new();

    public void Dispose() => _handler.Dispose();

    private ResendEmailSender CreateSender(string? apiKey = "re_test_key") =>
        new(
            new HttpClient(_handler) { BaseAddress = new Uri("https://api.resend.test/") },
            Options.Create(new EmailOptions { Provider = EmailOptions.ResendProvider, ResendApiKey = apiKey }),
            NullLogger<ResendEmailSender>.Instance);

    [Fact]
    public async Task SendAsync_Configured_PostsMessageWithBearerKey()
    {
        _handler.Reply(HttpStatusCode.OK, """{"id":"abc"}""");

        await CreateSender().SendAsync(Message, CancellationToken.None);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.resend.test/emails", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("re_test_key", request.Headers.Authorization.Parameter);

        using var body = JsonDocument.Parse(_handler.Bodies[0]!);
        Assert.Equal("AERai Seller <no-reply@aeraigroup.com>", body.RootElement.GetProperty("from").GetString());
        Assert.Equal("Some One <someone@aeraigroup.com>", body.RootElement.GetProperty("to")[0].GetString());
        Assert.Equal("Subject line", body.RootElement.GetProperty("subject").GetString());
        Assert.Equal("<p>Code 123456</p>", body.RootElement.GetProperty("html").GetString());
        Assert.Equal("Code 123456", body.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendAsync_Rejected_ThrowsWithResendMessageButNotBody()
    {
        _handler.Reply(HttpStatusCode.Forbidden, """{"statusCode":403,"name":"validation_error","message":"The aeraigroup.com domain is not verified."}""");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSender().SendAsync(Message, CancellationToken.None));

        Assert.Contains("403", error.Message, StringComparison.Ordinal);
        Assert.Contains("domain is not verified", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("123456", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_NoApiKey_DropsWithoutCallingResend()
    {
        var sender = CreateSender(apiKey: null);

        await sender.SendAsync(Message, CancellationToken.None);

        Assert.False(sender.IsConfigured);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task AddInfrastructure_ProviderResend_RegistersResendSender()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sql"] = "Server=unused;Database=unused",
            ["ConnectionStrings:RawStorage"] = "UseDevelopmentStorage=true",
            ["Email:Provider"] = "Resend",
            ["Email:ResendApiKey"] = "re_test_key",
        }).Build();

        await using var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration).AddInfrastructure(configuration).BuildServiceProvider();

        Assert.IsType<ResendEmailSender>(services.GetRequiredService<IEmailSender>());
    }
}
