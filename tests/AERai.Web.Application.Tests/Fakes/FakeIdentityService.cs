using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Security;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary><see cref="IIdentityService"/> with scriptable password-reset and sign-in-code behavior and user list; other members are unused.</summary>
internal sealed class FakeIdentityService : IIdentityService
{
    /// <summary>Tokens issued per email; an email missing here behaves like an unknown or locked account.</summary>
    public Dictionary<string, PasswordResetToken> Tokens { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> RequestedEmails { get; } = [];

    public Result NextResetResult { get; set; } = Result.Success();

    public List<UserSummary> Users { get; } = [];

    /// <summary>Codes issued per email; an email missing here behaves like an unknown, locked, or cooling-down account.</summary>
    public Dictionary<string, LoginCode> LoginCodes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<(string Email, string Code, bool RememberMe, int MaxAttempts)> CodeSignIns { get; } = [];

    public Result NextCodeSignInResult { get; set; } = Result.Success();

    public Task<LoginCode?> CreateLoginCodeAsync(string email, TimeSpan lifetime, TimeSpan resendCooldown, CancellationToken cancellationToken)
    {
        RequestedEmails.Add(email);
        return Task.FromResult(LoginCodes.GetValueOrDefault(email));
    }

    public Task<Result> CodeSignInAsync(string email, string code, bool rememberMe, int maxAttempts, CancellationToken cancellationToken)
    {
        CodeSignIns.Add((email, code, rememberMe, maxAttempts));
        return Task.FromResult(NextCodeSignInResult);
    }

    public Task<PasswordResetToken?> CreatePasswordResetTokenAsync(string email, CancellationToken cancellationToken)
    {
        RequestedEmails.Add(email);
        return Task.FromResult(Tokens.GetValueOrDefault(email));
    }

    public Task<Result> ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken cancellationToken) =>
        Task.FromResult(NextResetResult);

    public Task<Result> PasswordSignInAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task SignOutAsync() => throw new NotSupportedException();

    public Task<IReadOnlyList<UserSummary>> ListUsersAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UserSummary>>(Users);

    public Task<Result<string>> CreateUserAsync(CreateUserCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result> SetLockoutAsync(string userId, bool locked, string actingUserId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken) => throw new NotSupportedException();
}
