namespace AERai.Web.Application.Security;

/// <summary>
/// A user account as shown on the user administration page.
/// </summary>
/// <param name="Id">Identity user id.</param>
/// <param name="Email">Sign-in email.</param>
/// <param name="DisplayName">Friendly name shown in the UI.</param>
/// <param name="Roles">Assigned roles.</param>
/// <param name="IsLockedOut">Whether sign-in is currently blocked.</param>
public sealed record UserSummary(string Id, string Email, string DisplayName, IReadOnlyList<string> Roles, bool IsLockedOut);
