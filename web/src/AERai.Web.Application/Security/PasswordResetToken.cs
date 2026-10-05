namespace AERai.Web.Application.Security;

/// <summary>
/// A single-use password reset token for one user. The token is a credential: it is only ever put in
/// the reset link emailed to the account's own address, and is never logged or displayed.
/// </summary>
/// <param name="UserId">Identity user id.</param>
/// <param name="Email">The account's email (the link is only sent here).</param>
/// <param name="DisplayName">Name used in the email greeting.</param>
/// <param name="Token">URL-safe token value.</param>
public sealed record PasswordResetToken(string UserId, string Email, string DisplayName, string Token);
