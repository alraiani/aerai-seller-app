using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Security;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace AERai.Web.UI.Pages.Account;

/// <summary>
/// First step of "Login with code": emails a one-time code, then moves on to <see cref="VerifyCodeModel"/>.
/// Always moves on, whether or not the address has an account, so it can't be used to discover accounts.
/// Also handles the Resend button on the verify page.
/// </summary>
/// <param name="loginCodes">Sign-in code use cases.</param>
[EnableRateLimiting(RateLimits.LoginCode)]
public sealed class LoginWithCodeModel(ILoginCodeService loginCodes) : PageModel
{
    /// <summary>Posted form values.</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Local URL to return to after sign-in.</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>Shows the form; already-signed-in users go straight to the dashboard.</summary>
    /// <returns>The page or a redirect.</returns>
    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/") : Page();

    /// <summary>Sends a code (if eligible) and shows the code entry page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the verify page, or the form with validation errors.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await loginCodes.RequestCodeAsync(Input.Email, cancellationToken);

        // The email travels in TempData rather than the URL, so it doesn't land in history or logs.
        TempData[VerifyCodeModel.EmailKey] = Input.Email.Trim();
        if (Input.Resend)
        {
            TempData[StatusMessage.Success] = "If a code wasn't sent in the last minute, a new one is on its way. Use the newest email.";
        }

        return RedirectToPage("/Account/VerifyCode", new { ReturnUrl });
    }

    /// <summary>Form fields.</summary>
    public sealed class InputModel
    {
        /// <summary>Account email.</summary>
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>Whether this came from the verify page's Resend button (changes the status message only).</summary>
        public bool Resend { get; set; }
    }
}
