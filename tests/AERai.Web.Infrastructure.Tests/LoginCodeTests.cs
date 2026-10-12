using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// "Login with code" against real ASP.NET Core Identity and SQL Server: a code signs in once, wrong
/// guesses use it up and lock the account, and no code is issued for unknown, locked, or cooling-down accounts.
/// </summary>
public sealed class LoginCodeTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private static readonly TimeSpan Lifetime = LoginCodeService.CodeLifetime;
    private static readonly TimeSpan NoCooldown = TimeSpan.Zero;

    /// <summary>A scope with a request context, since signing in writes the auth cookie to the response.</summary>
    private AsyncServiceScope CreateRequestScope()
    {
        var scope = fixture.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        return scope;
    }

    private static async Task<(IIdentityService Identity, string UserId, string Email)> CreateUserAsync(AsyncServiceScope scope)
    {
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var email = $"code-{Guid.NewGuid():N}@aeraigroup.com";
        var created = await identity.CreateUserAsync(new CreateUserCommand(email, "Code Test", "Original-Pass-123", AppRoles.Viewer), CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error);
        return (identity, created.Value, email);
    }

    private static string Wrong(string code) => code == "000000" ? "111111" : "000000";

    [SqlFact]
    public async Task CodeSignIn_RightCode_SignsInExactlyOnce()
    {
        await using var scope = CreateRequestScope();
        var (identity, _, email) = await CreateUserAsync(scope);

        var code = await identity.CreateLoginCodeAsync(email, Lifetime, NoCooldown, CancellationToken.None);
        Assert.NotNull(code);
        Assert.Matches("^[0-9]{6}$", code.Code);

        var first = await identity.CodeSignInAsync(email, code.Code, rememberMe: false, LoginCodeService.MaxAttempts, CancellationToken.None);
        var reused = await identity.CodeSignInAsync(email, code.Code, rememberMe: false, LoginCodeService.MaxAttempts, CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error);
        Assert.Equal(LoginCodeService.InvalidCodeMessage, reused.Error);
    }

    [SqlFact]
    public async Task CodeSignIn_ExpiredCode_Fails()
    {
        await using var scope = CreateRequestScope();
        var (identity, _, email) = await CreateUserAsync(scope);
        var code = (await identity.CreateLoginCodeAsync(email, TimeSpan.Zero, NoCooldown, CancellationToken.None))!;

        var result = await identity.CodeSignInAsync(email, code.Code, rememberMe: false, LoginCodeService.MaxAttempts, CancellationToken.None);

        Assert.Equal(LoginCodeService.InvalidCodeMessage, result.Error);
    }

    [SqlFact]
    public async Task CodeSignIn_WrongGuesses_UseUpCodeAndLockAccount()
    {
        await using var scope = CreateRequestScope();
        var (identity, _, email) = await CreateUserAsync(scope);
        var code = (await identity.CreateLoginCodeAsync(email, Lifetime, NoCooldown, CancellationToken.None))!;

        var results = new List<string>();
        for (var i = 0; i < LoginCodeService.MaxAttempts; i++)
        {
            results.Add((await identity.CodeSignInAsync(email, Wrong(code.Code), rememberMe: false, LoginCodeService.MaxAttempts, CancellationToken.None)).Error);
        }

        var right = await identity.CodeSignInAsync(email, code.Code, rememberMe: false, LoginCodeService.MaxAttempts, CancellationToken.None);

        Assert.All(results[..^1], e => Assert.Equal(LoginCodeService.InvalidCodeMessage, e));
        Assert.Contains("locked", results[^1], StringComparison.Ordinal); // The fifth failure trips the account lockout.
        Assert.True(right.IsFailure);
        Assert.Null(await identity.CreateLoginCodeAsync(email, Lifetime, NoCooldown, CancellationToken.None)); // No codes while locked out.
    }

    [SqlFact]
    public async Task CreateLoginCode_WithinCooldown_IssuesNoNewCodeAndKeepsTheOld()
    {
        await using var scope = CreateRequestScope();
        var (identity, _, email) = await CreateUserAsync(scope);
        var first = (await identity.CreateLoginCodeAsync(email, Lifetime, LoginCodeService.ResendCooldown, CancellationToken.None))!;

        var second = await identity.CreateLoginCodeAsync(email, Lifetime, LoginCodeService.ResendCooldown, CancellationToken.None);
        var signIn = await identity.CodeSignInAsync(email, first.Code, rememberMe: false, LoginCodeService.MaxAttempts, CancellationToken.None);

        Assert.Null(second);
        Assert.True(signIn.IsSuccess, signIn.Error);
    }

    [SqlFact]
    public async Task CreateLoginCode_NewCode_ReplacesTheOld()
    {
        await using var scope = CreateRequestScope();
        var (identity, _, email) = await CreateUserAsync(scope);
        var first = (await identity.CreateLoginCodeAsync(email, Lifetime, NoCooldown, CancellationToken.None))!;
        var second = (await identity.CreateLoginCodeAsync(email, Lifetime, NoCooldown, CancellationToken.None))!;

        if (first.Code != second.Code)
        {
            var old = await identity.CodeSignInAsync(email, first.Code, rememberMe: false, LoginCodeService.MaxAttempts, CancellationToken.None);
            Assert.Equal(LoginCodeService.InvalidCodeMessage, old.Error);
        }

        var latest = await identity.CodeSignInAsync(email, second.Code, rememberMe: false, LoginCodeService.MaxAttempts, CancellationToken.None);
        Assert.True(latest.IsSuccess, latest.Error);
    }

    [SqlFact]
    public async Task CreateLoginCode_UnknownOrAdminLockedAccount_ReturnsNull()
    {
        await using var scope = CreateRequestScope();
        var (identity, userId, email) = await CreateUserAsync(scope);
        var (_, adminId, _) = await CreateUserAsync(scope);

        Assert.Null(await identity.CreateLoginCodeAsync("nobody@aeraigroup.com", Lifetime, NoCooldown, CancellationToken.None));

        var locked = await identity.SetLockoutAsync(userId, locked: true, actingUserId: adminId, CancellationToken.None);
        Assert.True(locked.IsSuccess, locked.Error);
        Assert.Null(await identity.CreateLoginCodeAsync(email, Lifetime, NoCooldown, CancellationToken.None));
    }
}
