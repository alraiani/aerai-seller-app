using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Security;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// Password reset against real ASP.NET Core Identity and SQL Server: tokens work once, can't be
/// tampered with, and are never issued for unknown or administrator-locked accounts.
/// </summary>
public sealed class PasswordResetTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private const string OriginalPassword = "Original-Pass-123";

    private static async Task<(IIdentityService Identity, string UserId, string Email)> CreateUserAsync(AsyncServiceScope scope)
    {
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var email = $"reset-{Guid.NewGuid():N}@aeraigroup.com";
        var created = await identity.CreateUserAsync(new CreateUserCommand(email, "Reset Test", OriginalPassword, AppRoles.Viewer), CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error);
        return (identity, created.Value, email);
    }

    [SqlFact]
    public async Task ResetPassword_ValidToken_WorksExactlyOnce()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var (identity, userId, email) = await CreateUserAsync(scope);

        var token = await identity.CreatePasswordResetTokenAsync(email, CancellationToken.None);
        Assert.NotNull(token);
        Assert.DoesNotContain("+", token.Token, StringComparison.Ordinal); // URL-safe.

        var first = await identity.ResetPasswordAsync(userId, token.Token, "Brand-New-Pass-456", CancellationToken.None);
        var reused = await identity.ResetPasswordAsync(userId, token.Token, "Another-Pass-789", CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error);
        Assert.Equal(PasswordResetService.InvalidLinkMessage, reused.Error);
    }

    [SqlFact]
    public async Task ResetPassword_TamperedOrForeignToken_IsRejected()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var (identity, userId, email) = await CreateUserAsync(scope);
        var (_, otherUserId, _) = await CreateUserAsync(scope);
        var token = (await identity.CreatePasswordResetTokenAsync(email, CancellationToken.None))!;

        var tampered = await identity.ResetPasswordAsync(userId, token.Token[..^2] + "AA", "Brand-New-Pass-456", CancellationToken.None);
        var garbage = await identity.ResetPasswordAsync(userId, "not base64 !!", "Brand-New-Pass-456", CancellationToken.None);
        var foreign = await identity.ResetPasswordAsync(otherUserId, token.Token, "Brand-New-Pass-456", CancellationToken.None);

        Assert.All([tampered, garbage, foreign], r => Assert.Equal(PasswordResetService.InvalidLinkMessage, r.Error));
    }

    [SqlFact]
    public async Task ResetPassword_WeakPassword_ExplainsPolicy()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var (identity, userId, email) = await CreateUserAsync(scope);
        var token = (await identity.CreatePasswordResetTokenAsync(email, CancellationToken.None))!;

        var result = await identity.ResetPasswordAsync(userId, token.Token, "short", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("12", result.Error, StringComparison.Ordinal);
    }

    [SqlFact]
    public async Task CreateToken_UnknownOrAdminLockedAccount_ReturnsNull()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var (identity, userId, email) = await CreateUserAsync(scope);
        var (_, adminId, _) = await CreateUserAsync(scope);

        Assert.Null(await identity.CreatePasswordResetTokenAsync("nobody@aeraigroup.com", CancellationToken.None));

        var locked = await identity.SetLockoutAsync(userId, locked: true, actingUserId: adminId, CancellationToken.None);
        Assert.True(locked.IsSuccess, locked.Error);
        Assert.Null(await identity.CreatePasswordResetTokenAsync(email, CancellationToken.None));
    }
}
