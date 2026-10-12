using AERai.Web.Application.Common;
using AERai.Web.Application.Security;
using AERai.Web.Application.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace AERai.Web.Application.Tests.Security;

public sealed class LoginCodeServiceTests
{
    private readonly FakeIdentityService _identity = new();
    private readonly FakeEmailSender _email = new();

    private LoginCodeService CreateService() => new(_identity, _email, NullLogger<LoginCodeService>.Instance);

    [Fact]
    public async Task RequestCodeAsync_KnownAccount_EmailsCodeToThatAccountOnly()
    {
        _identity.LoginCodes["ops@aeraigroup.com"] = new LoginCode("u1", "ops@aeraigroup.com", "Ops", "042137");

        await CreateService().RequestCodeAsync("  ops@aeraigroup.com ", CancellationToken.None);

        var message = Assert.Single(_email.Sent);
        Assert.Equal("ops@aeraigroup.com", message.To);
        Assert.Equal(LoginCodeEmail.Subject, message.Subject);
        Assert.DoesNotContain("042137", message.Subject, StringComparison.Ordinal);
        Assert.Contains("042137", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("042137", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("expires in 10 minutes", message.TextBody, StringComparison.Ordinal);
        Assert.Equal("ops@aeraigroup.com", Assert.Single(_identity.RequestedEmails)); // Trimmed before lookup.
    }

    [Fact]
    public async Task RequestCodeAsync_NoCodeIssued_SendsNothingAndDoesNotThrow()
    {
        await CreateService().RequestCodeAsync("nobody@example.com", CancellationToken.None);

        Assert.Empty(_email.Sent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RequestCodeAsync_BlankEmail_DoesNothing(string email)
    {
        await CreateService().RequestCodeAsync(email, CancellationToken.None);

        Assert.Empty(_identity.RequestedEmails);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public void LoginCodeEmail_HtmlEncodesDisplayName()
    {
        var message = LoginCodeEmail.Create(new LoginCode("u1", "a@aeraigroup.com", "<b>Al</b>", "123456"), LoginCodeService.CodeLifetime);

        Assert.Contains("&lt;b&gt;Al&lt;/b&gt;", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>Al</b>", message.HtmlBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("123 456", "123456")]
    [InlineData("123-456", "123456")]
    [InlineData(" 123456 ", "123456")]
    public async Task SignInWithCodeAsync_SpacesAndDashes_AreIgnored(string typed, string expected)
    {
        var result = await CreateService().SignInWithCodeAsync(" ops@aeraigroup.com ", typed, rememberMe: true, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var call = Assert.Single(_identity.CodeSignIns);
        Assert.Equal(("ops@aeraigroup.com", expected, true, LoginCodeService.MaxAttempts), call);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    [InlineData("")]
    public async Task SignInWithCodeAsync_Malformed_FailsWithoutCheckingIdentity(string typed)
    {
        var result = await CreateService().SignInWithCodeAsync("ops@aeraigroup.com", typed, rememberMe: false, CancellationToken.None);

        Assert.Equal(LoginCodeService.InvalidCodeMessage, result.Error);
        Assert.Empty(_identity.CodeSignIns);
    }

    [Fact]
    public async Task SignInWithCodeAsync_IdentityFailure_IsPassedThrough()
    {
        _identity.NextCodeSignInResult = Result.Failure("This account is locked.");

        var result = await CreateService().SignInWithCodeAsync("ops@aeraigroup.com", "123456", rememberMe: false, CancellationToken.None);

        Assert.Equal("This account is locked.", result.Error);
    }
}
