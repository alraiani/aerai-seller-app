using AERai.Web.Application.Common;
using AERai.Web.Application.Security;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Sign-in and user administration, abstracted so pages never depend on ASP.NET Core Identity types.
/// </summary>
public interface IIdentityService
{
    /// <summary>Validates credentials and, on success, issues the authentication cookie.</summary>
    /// <param name="email">Sign-in email.</param>
    /// <param name="password">Password.</param>
    /// <param name="rememberMe">Whether the cookie should persist across browser sessions.</param>
    /// <returns>Success, or a failure with a message safe to show (never reveals whether the email exists).</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result> PasswordSignInAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken);

    /// <summary>Clears the authentication cookie.</summary>
    Task SignOutAsync();

    /// <summary>Lists all users ordered by email.</summary>
    /// <returns>All user accounts.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<IReadOnlyList<UserSummary>> ListUsersAsync(CancellationToken cancellationToken);

    /// <summary>Creates a user with one role.</summary>
    /// <param name="command">Account details.</param>
    /// <returns>The new user id, or a failure listing the policy violations.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result<string>> CreateUserAsync(CreateUserCommand command, CancellationToken cancellationToken);

    /// <summary>Locks or unlocks a user's sign-in.</summary>
    /// <param name="userId">The user to change.</param>
    /// <param name="locked">Whether to lock (<see langword="true"/>) or unlock.</param>
    /// <param name="actingUserId">The administrator making the change; admins cannot lock themselves out.</param>
    /// <returns>Success, or a failure when the user does not exist or the change is not allowed.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result> SetLockoutAsync(string userId, bool locked, string actingUserId, CancellationToken cancellationToken);
}
