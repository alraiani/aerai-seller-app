using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Security;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace AERai.Web.UI.Pages.Account;

/// <summary>
/// Forgot-password form: emails a reset link. Always shows the same confirmation, whether or not
/// the address has an account, so it can't be used to discover accounts.
/// </summary>
/// <param name="passwordReset">Reset use cases.</param>
/// <param name="configuration">App configuration (public base URL for the link).</param>
[EnableRateLimiting(RateLimits.PasswordReset)]
public sealed class ForgotPasswordModel(IPasswordResetService passwordReset, IConfiguration configuration) : PageModel
{
    /// <summary>Posted form values.</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Shows the form.</summary>
    public void OnGet()
    {
    }

    /// <summary>Sends the link (if eligible) and shows the confirmation.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the confirmation page, or the form with validation errors.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await passwordReset.RequestResetAsync(
            Input.Email,
            token => PublicUrl.Page(Url, configuration, Request, "/Account/ResetPassword", new { userId = token.UserId, token = token.Token }),
            cancellationToken);

        return RedirectToPage("/Account/ForgotPasswordConfirmation");
    }

    /// <summary>Form fields.</summary>
    public sealed class InputModel
    {
        /// <summary>Account email.</summary>
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
