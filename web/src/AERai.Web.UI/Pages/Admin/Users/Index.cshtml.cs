using System.Security.Claims;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Security;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Admin.Users;

/// <summary>
/// User administration: list accounts and lock/unlock them. Admins only (folder convention).
/// </summary>
/// <param name="identity">Identity service.</param>
public sealed class IndexModel(IIdentityService identity) : PageModel
{
    /// <summary>All users.</summary>
    public IReadOnlyList<UserSummary> Users { get; private set; } = [];

    /// <summary>The signed-in admin's user id (used to hide "Lock" on their own row).</summary>
    public string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>Loads the user list.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the list is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Users = await identity.ListUsersAsync(cancellationToken);
    }

    /// <summary>Locks or unlocks a user.</summary>
    /// <param name="userId">Target user.</param>
    /// <param name="locked">Desired state.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list with a status message.</returns>
    public async Task<IActionResult> OnPostSetLockoutAsync(string userId, bool locked, CancellationToken cancellationToken)
    {
        var result = await identity.SetLockoutAsync(userId, locked, CurrentUserId, cancellationToken);
        TempData[result.IsSuccess ? StatusMessage.Success : StatusMessage.Error] =
            result.IsSuccess ? (locked ? "User locked." : "User unlocked.") : result.Error;
        return RedirectToPage();
    }
}
