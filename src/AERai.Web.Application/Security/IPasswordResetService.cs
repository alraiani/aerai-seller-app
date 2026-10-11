using AERai.Web.Application.Common;

namespace AERai.Web.Application.Security;

/// <summary>
/// Forgot-password flow: email a single-use reset link, then set a new password with it.
/// </summary>
public interface IPasswordResetService
{
    /// <summary>
    /// Emails a reset link if the address belongs to an account that may reset its password.
    /// </summary>
    /// <param name="email">The address entered on the forgot-password form.</param>
    /// <param name="buildResetLink">Builds the absolute reset-page URL for a token (the UI owns routing).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when any email has been handed to the mail server.</returns>
    /// <remarks>
    /// Deliberately gives no result: the page shows the same message whether or not the account
    /// exists, so the form can't be used to discover which emails have accounts.
    /// </remarks>
    Task RequestResetAsync(string email, Func<PasswordResetToken, string> buildResetLink, CancellationToken cancellationToken);

    /// <summary>Sets a new password from a reset link.</summary>
    /// <param name="userId">User id from the link.</param>
    /// <param name="token">Token from the link.</param>
    /// <param name="newPassword">New password.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a failure with a message safe to show.</returns>
    Task<Result> ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken cancellationToken);
}
