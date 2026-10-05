using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AERai.Web.Application.Abstractions;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Account;

/// <summary>
/// Lets the signed-in user change their password (requires the current one). Other sessions are
/// signed out; this one stays signed in.
/// </summary>
/// <param name="identity">Identity operations.</param>
public sealed class ChangePasswordModel(IIdentityService identity) : PageModel
{
    /// <summary>Posted form values.</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Shows the form.</summary>
    public void OnGet()
    {
    }

    /// <summary>Changes the password.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back here with a confirmation, or the form with errors.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var result = await identity.ChangePasswordAsync(userId, Input.CurrentPassword, Input.NewPassword, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return Page();
        }

        TempData[StatusMessage.Success] = "Your password has been changed. Any other devices signed in to your account will be signed out within a minute.";
        return RedirectToPage();
    }

    /// <summary>Form fields.</summary>
    public sealed class InputModel
    {
        /// <summary>Current password.</summary>
        [Required, DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

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
