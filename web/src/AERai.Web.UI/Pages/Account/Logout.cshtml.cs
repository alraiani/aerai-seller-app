using AERai.Web.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Account;

/// <summary>
/// Signs the user out. POST-only (with antiforgery) so a third-party link or image can't log users out.
/// </summary>
/// <param name="identity">Sign-in service.</param>
public sealed class LogoutModel(IIdentityService identity) : PageModel
{
    /// <summary>A GET just returns to the dashboard; sign-out requires the POST.</summary>
    /// <returns>A redirect.</returns>
    public IActionResult OnGet() => RedirectToPage("/Index");

    /// <summary>Clears the authentication cookie.</summary>
    /// <returns>A redirect to the sign-in page.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        await identity.SignOutAsync();
        return RedirectToPage("/Account/Login");
    }
}
