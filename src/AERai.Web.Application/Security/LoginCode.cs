namespace AERai.Web.Application.Security;

/// <summary>
/// A one-time sign-in code for one user. The code is a credential: it is only ever put in the email
/// sent to the account's own address, and is never logged or displayed.
/// </summary>
/// <param name="UserId">Identity user id.</param>
/// <param name="Email">The account's email (the code is only sent here).</param>
/// <param name="DisplayName">Name used in the email greeting.</param>
/// <param name="Code">The six-digit code.</param>
public sealed record LoginCode(string UserId, string Email, string DisplayName, string Code);
