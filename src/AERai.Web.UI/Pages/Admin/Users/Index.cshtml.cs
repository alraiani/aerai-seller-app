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
    /// <summary>Optional search from <c>?q=</c>: every word must appear in the name, email, or a role.</summary>
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    /// <summary>The users matching <see cref="Search"/> (all of them without one).</summary>
    public IReadOnlyList<UserSummary> Users { get; private set; } = [];

    /// <summary>How many users exist, whatever the search.</summary>
    public int TotalUsers { get; private set; }

    /// <summary>The signed-in admin's user id (used to hide "Lock" on their own row).</summary>
    public string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>Loads the user list.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the list is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var all = await identity.ListUsersAsync(cancellationToken);
        TotalUsers = all.Count;
        var words = (Search ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // A handful of accounts: filtering the list here is simpler than a query, and Identity owns the users.
        Users = all.Where(u => words.All(w =>
                u.DisplayName.Contains(w, StringComparison.OrdinalIgnoreCase)
                || u.Email.Contains(w, StringComparison.OrdinalIgnoreCase)
                || u.Roles.Any(r => r.Contains(w, StringComparison.OrdinalIgnoreCase))))
            .ToList();
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
