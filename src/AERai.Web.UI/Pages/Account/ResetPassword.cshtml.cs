using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Security;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Account;

/// <summary>
/// Choose a new password from an emailed reset link (<c>?userId=…&amp;token=…</c>).
/// </summary>
/// <param name="passwordReset">Reset use cases.</param>
// The URL carries a credential (the token): never cache this page anywhere.
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ResetPasswordModel(IPasswordResetService passwordReset) : PageModel
{
    /// <summary>User id from the link.</summary>
    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    /// <summary>Token from the link.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    /// <summary>Posted form values.</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Whether the link is missing its parameters (shows a "request a new link" message).</summary>
    public bool LinkIncomplete => string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(Token);

    /// <summary>Shows the form.</summary>
    public void OnGet()
    {
    }

    /// <summary>Sets the new password.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to sign-in on success; the form with errors otherwise.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (LinkIncomplete || !ModelState.IsValid)
        {
            return Page();
        }

        var result = await passwordReset.ResetPasswordAsync(UserId!, Token!, Input.NewPassword, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return Page();
        }

        TempData[StatusMessage.Success] = "Your password has been changed. Sign in with your new password.";
        return RedirectToPage("/Account/Login");
    }

    /// <summary>Form fields.</summary>
    public sealed class InputModel
    {
        /// <summary>New password.</summary>
        [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 12)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        /// <summary>Confirmation.</summary>
        [Required, DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The passwords don't match.")]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
