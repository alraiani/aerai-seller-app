using System.Net.Http.Json;
using System.Text.Json;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>Sends a real message through Mailpit (docker compose) and reads it back via Mailpit's API.</summary>
public sealed class SmtpEmailSenderTests
{
    [MailpitFact]
    public async Task SendAsync_DeliversHtmlAndTextToMailpit()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sql"] = "Server=unused;Database=unused",
            ["ConnectionStrings:RawStorage"] = "UseDevelopmentStorage=true",
            ["Email:Host"] = "localhost",
            ["Email:Port"] = "1025",
            ["Email:Security"] = "None",
        }).Build();

        await using var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration).AddInfrastructure(configuration).BuildServiceProvider();
        var subject = $"Test {Guid.NewGuid():N}";

        await services.GetRequiredService<IEmailSender>().SendAsync(
            new EmailMessage("someone@aeraigroup.com", "Someone", subject, "<p>Hello <b>there</b></p>", "Hello there"), CancellationToken.None);

        using var http = new HttpClient { BaseAddress = new Uri(MailpitFactAttribute.ApiBase) };
        var search = await http.GetFromJsonAsync<JsonElement>($"api/v1/search?query={Uri.EscapeDataString($"subject:\"{subject}\"")}");
        var message = Assert.Single(search.GetProperty("messages").EnumerateArray());
        Assert.Equal("someone@aeraigroup.com", message.GetProperty("To")[0].GetProperty("Address").GetString());
        Assert.Equal("no-reply@aeraigroup.com", message.GetProperty("From").GetProperty("Address").GetString());
    }
}

/// <summary>Runs only when <c>AERAI_TEST_MAILPIT</c> is set (e.g. <c>http://localhost:8025/</c>).</summary>
public sealed class MailpitFactAttribute : FactAttribute
{
    /// <summary>Mailpit API base URL from the environment.</summary>
    public static string ApiBase => Environment.GetEnvironmentVariable("AERAI_TEST_MAILPIT") ?? "http://localhost:8025/";

    public MailpitFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AERAI_TEST_MAILPIT")))
        {
            Skip = "Set AERAI_TEST_MAILPIT (e.g. http://localhost:8025/) to run Mailpit email tests.";
        }
    }
}
