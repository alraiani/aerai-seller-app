using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Security;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
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
            return Result.Failure("This account is locked. Try again later or contact an administrator.");
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

        // Invalidates existing cookies so a lock takes effect on the user's next request.
        await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);

        LogLockoutChanged(userId, locked, actingUserId);
        return Result.Success();
    }

    /// <summary>Joins Identity's error descriptions into one user-presentable message.</summary>
    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Description));

    [LoggerMessage(Level = LogLevel.Information, Message = "User {Email} signed in")]
    private partial void LogSignedIn(string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in failed for {Email}")]
    private partial void LogSignInFailed(string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in blocked for locked-out account {Email}")]
    private partial void LogLockedOut(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created user {UserId} with role {Role}")]
    private partial void LogUserCreated(string userId, string role);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} lockout set to {Locked} by {ActingUserId}")]
    private partial void LogLockoutChanged(string userId, bool locked, string actingUserId);
}
