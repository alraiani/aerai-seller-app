using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Security;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace AERai.Web.UI.Pages.Account;

/// <summary>
/// Second step of "Login with code": enter the emailed code to sign in. Reached from
/// <see cref="LoginWithCodeModel"/>, which passes the email in TempData.
/// </summary>
/// <param name="loginCodes">Sign-in code use cases.</param>
[EnableRateLimiting(RateLimits.LoginCodeVerify)]
public sealed class VerifyCodeModel(ILoginCodeService loginCodes) : PageModel
{
    /// <summary>TempData key carrying the email from the request page.</summary>
    public const string EmailKey = "LoginCodeEmail";

    /// <summary>Posted form values.</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Local URL to return to after sign-in.</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>Shows the code form for the email just entered; without one, starts over.</summary>
    /// <returns>The page or a redirect.</returns>
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(SafeReturnUrl);
        }

        if (TempData[EmailKey] is not string email || string.IsNullOrWhiteSpace(email))
        {
            return RedirectToPage("/Account/LoginWithCode", new { ReturnUrl });
        }

        Input.Email = email;
        return Page();
    }

    /// <summary>Checks the code and signs the user in.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect on success; the page with an error otherwise.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Input.Email))
        {
            return RedirectToPage("/Account/LoginWithCode", new { ReturnUrl });
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await loginCodes.SignInWithCodeAsync(Input.Email, Input.Code, Input.RememberMe, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            Input.Code = string.Empty;
            ModelState.Remove($"{nameof(Input)}.{nameof(InputModel.Code)}");
            return Page();
        }

        return LocalRedirect(SafeReturnUrl);
    }

    /// <summary>The return URL if it is local; otherwise the dashboard (prevents open redirects).</summary>
    private string SafeReturnUrl => Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/";

    /// <summary>Form fields.</summary>
    public sealed class InputModel
    {
        /// <summary>The account email the code was sent to (hidden field).</summary>
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>The emailed code.</summary>
        [Required(ErrorMessage = "Enter the 6-digit code from the email.")]
        [Display(Name = "Code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>Keep the user signed in across browser restarts.</summary>
        [Display(Name = "Keep me signed in")]
        public bool RememberMe { get; set; }
    }
}
