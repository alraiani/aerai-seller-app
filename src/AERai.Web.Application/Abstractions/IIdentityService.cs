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

    /// <summary>Changes the signed-in user's password after verifying the current one.</summary>
    /// <param name="userId">The signed-in user.</param>
    /// <param name="currentPassword">Their current password.</param>
    /// <param name="newPassword">The new password (must satisfy the password policy).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a failure describing what to fix (wrong current password, policy violations).</returns>
    /// <remarks>Other sessions for the account are signed out within a minute; the current session stays signed in.</remarks>
    Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a password reset token for an email address, if it belongs to an account that may reset
    /// its password.
    /// </summary>
    /// <param name="email">The address someone entered on the forgot-password form.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The token, or <see langword="null"/> when no account exists or an administrator has locked it.</returns>
    Task<PasswordResetToken?> CreatePasswordResetTokenAsync(string email, CancellationToken cancellationToken);

    /// <summary>Sets a new password using a token from <see cref="CreatePasswordResetTokenAsync"/>.</summary>
    /// <param name="userId">User id from the reset link.</param>
    /// <param name="token">Token from the reset link.</param>
    /// <param name="newPassword">The new password (must satisfy the password policy).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a failure when the link is invalid/expired/used or the password breaks the policy.</returns>
    /// <remarks>
    /// Tokens are single-use: a successful reset changes the account's security stamp, which also
    /// invalidates every other outstanding reset link and signs out every existing session.
    /// </remarks>
    Task<Result> ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken cancellationToken);

    /// <summary>
    /// Issues a one-time sign-in code for an email address, replacing any earlier code, if it belongs to
    /// an account that may sign in.
    /// </summary>
    /// <param name="email">The address someone entered on the login-with-code form.</param>
    /// <param name="lifetime">How long the code works.</param>
    /// <param name="resendCooldown">No new code is issued within this time of the previous one.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The code, or <see langword="null"/> when no account exists, it is locked (by an administrator or
    /// failed attempts), or the previous code was issued within <paramref name="resendCooldown"/>.
    /// </returns>
    /// <remarks>Only a hash of the code is stored.</remarks>
    Task<LoginCode?> CreateLoginCodeAsync(string email, TimeSpan lifetime, TimeSpan resendCooldown, CancellationToken cancellationToken);

    /// <summary>Checks a code from <see cref="CreateLoginCodeAsync"/> and, if it matches, issues the authentication cookie.</summary>
    /// <param name="email">The account's email.</param>
    /// <param name="code">The six digits entered.</param>
    /// <param name="rememberMe">Whether the cookie should persist across browser sessions.</param>
    /// <param name="maxAttempts">Wrong guesses after which the code is discarded.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a failure with a message safe to show (never says which check failed).</returns>
    /// <remarks>
    /// A code works once. Every wrong guess also counts as a failed sign-in, so the account's normal
    /// lockout applies to codes and passwords alike.
    /// </remarks>
    Task<Result> CodeSignInAsync(string email, string code, bool rememberMe, int maxAttempts, CancellationToken cancellationToken);

    /// <summary>Locks or unlocks a user's sign-in.</summary>
    /// <param name="userId">The user to change.</param>
    /// <param name="locked">Whether to lock (<see langword="true"/>) or unlock.</param>
    /// <param name="actingUserId">The administrator making the change; admins cannot lock themselves out.</param>
    /// <returns>Success, or a failure when the user does not exist or the change is not allowed.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result> SetLockoutAsync(string userId, bool locked, string actingUserId, CancellationToken cancellationToken);
}
