using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Account;

/// <summary>
/// Sign-in page. The only page reachable without authentication (besides error pages).
/// </summary>
/// <param name="identity">Sign-in service.</param>
public sealed class LoginModel(IIdentityService identity) : PageModel
{
    /// <summary>Posted credentials.</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Local URL to return to after sign-in.</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>Shows the form; already-signed-in users go straight to the dashboard.</summary>
    /// <returns>The page or a redirect.</returns>
    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? LocalRedirect(SafeReturnUrl) : Page();

    /// <summary>Validates credentials and signs the user in.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect on success; the page with an error otherwise.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await identity.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return Page();
        }

        return LocalRedirect(SafeReturnUrl);
    }

    /// <summary>
    /// The return URL if it is local; otherwise the dashboard. Prevents open-redirect attacks via
    /// a crafted <c>?ReturnUrl=https://evil.example</c>.
    /// </summary>
    private string SafeReturnUrl => Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/";

    /// <summary>Sign-in form fields.</summary>
    public sealed class InputModel
    {
        /// <summary>Account email.</summary>
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>Account password.</summary>
        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        /// <summary>Keep the user signed in across browser restarts.</summary>
        [Display(Name = "Keep me signed in")]
        public bool RememberMe { get; set; }
    }
}
