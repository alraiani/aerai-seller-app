using AERai.Web.Application.Common;

namespace AERai.Web.Application.Security;

/// <summary>
/// "Login with code": email a one-time six-digit code, then sign in with it instead of a password.
/// </summary>
public interface ILoginCodeService
{
    /// <summary>Emails a sign-in code if the address belongs to an account that may sign in.</summary>
    /// <param name="email">The address entered on the form.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when any email has been handed to the mail provider.</returns>
    /// <remarks>
    /// Deliberately gives no result: the page shows the same message whether or not the account exists
    /// (or a code was just sent and the resend cooldown applies), so the form can't reveal accounts.
    /// </remarks>
    Task RequestCodeAsync(string email, CancellationToken cancellationToken);

    /// <summary>Signs in with an emailed code and, on success, issues the authentication cookie.</summary>
    /// <param name="email">The account's email.</param>
    /// <param name="code">The code as typed (spaces and dashes are ignored).</param>
    /// <param name="rememberMe">Whether the cookie should persist across browser sessions.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a failure with a message safe to show.</returns>
    Task<Result> SignInWithCodeAsync(string email, string code, bool rememberMe, CancellationToken cancellationToken);
}
