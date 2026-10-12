using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Security;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Infrastructure.Identity;

/// <summary>
/// <see cref="IIdentityService"/> backed by ASP.NET Core Identity.
/// </summary>
/// <param name="signInManager">Identity sign-in manager (issues the auth cookie).</param>
/// <param name="userManager">Identity user manager.</param>
/// <param name="dbContext">Used for the joined user/role list, avoiding one query per user.</param>
/// <param name="timeProvider">Clock for lockout comparisons.</param>
/// <param name="logger">Logger.</param>
internal sealed partial class IdentityService(
    SignInManager<AppUser> signInManager,
    UserManager<AppUser> userManager,
    AppDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<IdentityService> logger) : IIdentityService
{
    /// <summary>Same message for unknown email and wrong password, so sign-in can't be used to enumerate accounts.</summary>
    private const string InvalidCredentials = "Invalid email or password.";

    private const string LockedOutMessage = "This account is locked. Try again later or contact an administrator.";

    /// <summary>Login provider and name of the outstanding sign-in code in Identity's user-token table (one per user).</summary>
    private const string LoginCodeProvider = "AERai";

    private const string LoginCodeName = "LoginCode";

    /// <summary>Identity's authentication-method claim value for code sign-ins (shown in sign-in logs).</summary>
    private const string LoginCodeAuthenticationMethod = "otp";

    /// <inheritdoc/>
    public async Task<Result> PasswordSignInAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken)
    {
        var result = await signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true).ConfigureAwait(false);

        if (result.Succeeded)
        {
            LogSignedIn(email);
            return Result.Success();
        }

        if (result.IsLockedOut)
        {
            LogLockedOut(email);
            return Result.Failure(LockedOutMessage);
        }

        LogSignInFailed(email);
        return Result.Failure(InvalidCredentials);
    }

    /// <inheritdoc/>
    public Task SignOutAsync() => signInManager.SignOutAsync();

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserSummary>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                IsLockedOut = u.LockoutEnd != null && u.LockoutEnd > now,
                Roles = dbContext.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(dbContext.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
                    .ToList(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return users
            .Select(u => new UserSummary(u.Id, u.Email ?? string.Empty, u.DisplayName, u.Roles, u.IsLockedOut))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<Result<string>> CreateUserAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!AppRoles.All.Contains(command.Role, StringComparer.Ordinal))
        {
            return Result.Failure<string>($"Unknown role '{command.Role}'.");
        }

        var user = new AppUser
        {
            UserName = command.Email,
            Email = command.Email,
            DisplayName = command.DisplayName,

            // Accounts are provisioned by an administrator, who vouches for the address.
            EmailConfirmed = true,
        };

        var created = await userManager.CreateAsync(user, command.Password).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            return Result.Failure<string>(Describe(created));
        }

        var assigned = await userManager.AddToRoleAsync(user, command.Role).ConfigureAwait(false);
        if (!assigned.Succeeded)
        {
            return Result.Failure<string>(Describe(assigned));
        }

        LogUserCreated(user.Id, command.Role);
        return Result.Success(user.Id);
    }

    /// <inheritdoc/>
    public async Task<Result> SetLockoutAsync(string userId, bool locked, string actingUserId, CancellationToken cancellationToken)
    {
        if (locked && string.Equals(userId, actingUserId, StringComparison.Ordinal))
        {
            return Result.Failure("You cannot lock your own account.");
        }

        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure("User not found.");
        }

        // DateTimeOffset.MaxValue is Identity's convention for an indefinite, admin-applied lock.
        var lockoutEnd = locked ? DateTimeOffset.MaxValue : (DateTimeOffset?)null;
        var result = await userManager.SetLockoutEndDateAsync(user, lockoutEnd).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return Result.Failure(Describe(result));
        }

        if (!locked)
        {
            await userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
        }

        // Invalidates existing cookies: the lock takes effect within the 1-minute security stamp check.
        await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);

        LogLockoutChanged(userId, locked, actingUserId);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure("Your account could not be found. Sign in again.");
        }

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            // Identity's own message for a wrong current password is "Incorrect password."; keep it plain.
            return Result.Failure(result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                ? "Your current password is incorrect."
                : Describe(result));
        }

        // Changing the password rotates the security stamp, which would sign this session out on its
        // next check; re-issue this session's cookie so only *other* sessions are signed out.
        await signInManager.RefreshSignInAsync(user).ConfigureAwait(false);
        LogPasswordChanged(userId);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<PasswordResetToken?> CreatePasswordResetTokenAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);

        // Admin-locked accounts (LockoutEnd = MaxValue, see SetLockoutAsync) must not be able to get back
        // in through a reset; temporary failed-attempt lockouts may, since resetting is the fix for them.
        if (user is null || user.LockoutEnd == DateTimeOffset.MaxValue)
        {
            return null;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);

        // Identity tokens contain characters (+, /, =) that don't survive a URL; Base64Url makes them link-safe.
        var urlSafe = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        return new PasswordResetToken(user.Id, user.Email ?? email, user.DisplayName, urlSafe);
    }

    /// <inheritdoc/>
    public async Task<Result> ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null || user.LockoutEnd == DateTimeOffset.MaxValue || !TryDecode(token, out var decoded))
        {
            return Result.Failure(PasswordResetService.InvalidLinkMessage);
        }

        var result = await userManager.ResetPasswordAsync(user, decoded, newPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return Result.Failure(result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? PasswordResetService.InvalidLinkMessage
                : Describe(result));
        }

        // Proving control of the mailbox is enough to clear a temporary failed-attempt lockout.
        await userManager.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
        await userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<LoginCode?> CreateLoginCodeAsync(string email, TimeSpan lifetime, TimeSpan resendCooldown, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);

        // Unlike a reset, a code is a way in, so any lockout (admin or failed attempts) blocks it.
        if (user is null || await userManager.IsLockedOutAsync(user).ConfigureAwait(false))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var previous = StoredLoginCode.Parse(await userManager.GetAuthenticationTokenAsync(user, LoginCodeProvider, LoginCodeName).ConfigureAwait(false));
        if (previous is not null && now - previous.IssuedAt < resendCooldown)
        {
            return null;
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var stored = new StoredLoginCode(userManager.PasswordHasher.HashPassword(user, code), now, now + lifetime, Attempts: 0);
        await SaveLoginCodeAsync(user, stored).ConfigureAwait(false);
        return new LoginCode(user.Id, user.Email ?? email, user.DisplayName, code);
    }

    /// <inheritdoc/>
    public async Task<Result> CodeSignInAsync(string email, string code, bool rememberMe, int maxAttempts, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(LoginCodeService.InvalidCodeMessage);
        }

        if (await userManager.IsLockedOutAsync(user).ConfigureAwait(false))
        {
            LogLockedOut(email);
            return Result.Failure(LockedOutMessage);
        }

        var stored = StoredLoginCode.Parse(await userManager.GetAuthenticationTokenAsync(user, LoginCodeProvider, LoginCodeName).ConfigureAwait(false));
        if (stored is null || timeProvider.GetUtcNow() >= stored.ExpiresAt || stored.Attempts >= maxAttempts)
        {
            return Result.Failure(LoginCodeService.InvalidCodeMessage);
        }

        if (userManager.PasswordHasher.VerifyHashedPassword(user, stored.Hash, code) == PasswordVerificationResult.Failed)
        {
            var attempts = stored.Attempts + 1;
            if (attempts >= maxAttempts)
            {
                await userManager.RemoveAuthenticationTokenAsync(user, LoginCodeProvider, LoginCodeName).ConfigureAwait(false);
            }
            else
            {
                await SaveLoginCodeAsync(user, stored with { Attempts = attempts }).ConfigureAwait(false);
            }

            // Counts toward the same lockout as wrong passwords.
            await userManager.AccessFailedAsync(user).ConfigureAwait(false);
            LogSignInFailed(email);
            return await userManager.IsLockedOutAsync(user).ConfigureAwait(false)
                ? Result.Failure(LockedOutMessage)
                : Result.Failure(LoginCodeService.InvalidCodeMessage);
        }

        // Single use: the code is gone before the cookie is issued.
        await userManager.RemoveAuthenticationTokenAsync(user, LoginCodeProvider, LoginCodeName).ConfigureAwait(false);
        await userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
        await signInManager.SignInAsync(user, rememberMe, LoginCodeAuthenticationMethod).ConfigureAwait(false);
        LogSignedInWithCode(email);
        return Result.Success();
    }

    private Task<IdentityResult> SaveLoginCodeAsync(AppUser user, StoredLoginCode stored) =>
        userManager.SetAuthenticationTokenAsync(user, LoginCodeProvider, LoginCodeName, stored.Format());

    private static bool TryDecode(string token, out string decoded)
    {
        try
        {
            decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            return true;
        }
        catch (FormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }

    /// <summary>Joins Identity's error descriptions into one user-presentable message.</summary>
    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Description));

    [LoggerMessage(Level = LogLevel.Information, Message = "User {Email} signed in")]
    private partial void LogSignedIn(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {Email} signed in with an emailed code")]
    private partial void LogSignedInWithCode(string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in failed for {Email}")]
    private partial void LogSignInFailed(string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in blocked for locked-out account {Email}")]
    private partial void LogLockedOut(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created user {UserId} with role {Role}")]
    private partial void LogUserCreated(string userId, string role);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} changed their password")]
    private partial void LogPasswordChanged(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} lockout set to {Locked} by {ActingUserId}")]
    private partial void LogLockoutChanged(string userId, bool locked, string actingUserId);

    /// <summary>
    /// The outstanding sign-in code as kept in the user-token value: a hash of the code (never the code
    /// itself), when it was issued and expires, and how many wrong guesses it has had.
    /// </summary>
    private sealed record StoredLoginCode(string Hash, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, int Attempts)
    {
        // The hash is Base64 (no '|'), so a pipe-separated value round-trips safely.
        public string Format() => string.Join(
            '|', Hash, IssuedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ExpiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), Attempts.ToString(CultureInfo.InvariantCulture));

        public static StoredLoginCode? Parse(string? value)
        {
            var parts = value?.Split('|');
            return parts is { Length: 4 }
                && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var issued)
                && long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
                && int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var attempts)
                    ? new StoredLoginCode(parts[0], DateTimeOffset.FromUnixTimeSeconds(issued), DateTimeOffset.FromUnixTimeSeconds(expires), attempts)
                    : null;
        }
    }
}
