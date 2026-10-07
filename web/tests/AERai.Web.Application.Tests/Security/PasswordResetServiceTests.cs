using AERai.Web.Application.Common;
using AERai.Web.Application.Security;
using AERai.Web.Application.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace AERai.Web.Application.Tests.Security;

public sealed class PasswordResetServiceTests
{
    private readonly FakeIdentityService _identity = new();
    private readonly FakeEmailSender _email = new();

    private PasswordResetService CreateService() => new(_identity, _email, NullLogger<PasswordResetService>.Instance);

    private static string Link(PasswordResetToken t) => $"https://seller.aeraigroup.com/Account/ResetPassword?userId={t.UserId}&token={t.Token}";

    [Fact]
    public async Task RequestResetAsync_KnownAccount_EmailsLinkToThatAccountOnly()
    {
        _identity.Tokens["ops@aeraigroup.com"] = new PasswordResetToken("u1", "ops@aeraigroup.com", "Ops", "tok123");

        await CreateService().RequestResetAsync("  ops@aeraigroup.com ", Link, CancellationToken.None);

        var message = Assert.Single(_email.Sent);
        Assert.Equal("ops@aeraigroup.com", message.To);
        Assert.Equal(PasswordResetEmail.Subject, message.Subject);
        Assert.Contains("token=tok123", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("expires in 1 hour", message.TextBody, StringComparison.Ordinal);
        Assert.Equal("ops@aeraigroup.com", Assert.Single(_identity.RequestedEmails)); // Trimmed before lookup.
    }

    [Fact]
    public async Task RequestResetAsync_UnknownOrLockedAccount_SendsNothingAndDoesNotThrow()
    {
        await CreateService().RequestResetAsync("nobody@example.com", Link, CancellationToken.None);

        Assert.Empty(_email.Sent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RequestResetAsync_BlankEmail_DoesNothing(string email)
    {
        await CreateService().RequestResetAsync(email, Link, CancellationToken.None);

        Assert.Empty(_identity.RequestedEmails);
        Assert.Empty(_email.Sent);
    }

    [Theory]
    [InlineData("", "tok")]
    [InlineData("u1", "")]
    public async Task ResetPasswordAsync_MissingLinkParts_FailsWithGenericMessage(string userId, string token)
    {
        var result = await CreateService().ResetPasswordAsync(userId, token, "NewPassword123", CancellationToken.None);

        Assert.Equal(PasswordResetService.InvalidLinkMessage, result.Error);
    }

    [Fact]
    public async Task ResetPasswordAsync_PassesThroughIdentityOutcome()
    {
        _identity.NextResetResult = Result.Failure("Passwords must have at least one digit ('0'-'9').");

        var result = await CreateService().ResetPasswordAsync("u1", "tok", "NoDigitsHereAtAll", CancellationToken.None);

        Assert.Contains("digit", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Email_HtmlEncodesUserSuppliedValues()
    {
        var token = new PasswordResetToken("u1", "x@aeraigroup.com", "<script>alert(1)</script>", "t");

        var message = PasswordResetEmail.Create(token, "https://seller.aeraigroup.com/r?a=1&b=2", TimeSpan.FromHours(1));

        Assert.DoesNotContain("<script>", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("a=1&amp;b=2", message.HtmlBody, StringComparison.Ordinal);
    }
}
