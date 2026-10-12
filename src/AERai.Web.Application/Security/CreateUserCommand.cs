namespace AERai.Web.Application.Security;

/// <summary>
/// Request to create a user account. Accounts are created by Admins; there is no self-registration.
/// </summary>
/// <param name="Email">Sign-in email (must be unique).</param>
/// <param name="DisplayName">Friendly name shown in the UI.</param>
/// <param name="Password">Initial password; must satisfy the Identity password policy.</param>
/// <param name="Role">One of <see cref="AppRoles.All"/>.</param>
public sealed record CreateUserCommand(string Email, string DisplayName, string Password, string Role);
