using Microsoft.AspNetCore.Identity;

namespace AERai.Web.Infrastructure.Identity;

/// <summary>
/// Application user account stored in <c>auth.Users</c>.
/// </summary>
public sealed class AppUser : IdentityUser
{
    /// <summary>Friendly name shown in the UI header and user list.</summary>
    [PersonalData]
    public string DisplayName { get; set; } = string.Empty;
}
