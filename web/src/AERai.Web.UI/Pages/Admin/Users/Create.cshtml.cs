using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Security;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Admin.Users;

/// <summary>
/// Creates a user account with one role. Admins only (folder convention).
/// </summary>
/// <param name="identity">Identity service.</param>
public sealed class CreateModel(IIdentityService identity) : PageModel
{
    /// <summary>Posted form values.</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Roles offered in the role picker.</summary>
    public IReadOnlyList<string> Roles => AppRoles.All;

    /// <summary>Shows the form.</summary>
    public void OnGet()
    {
    }

    /// <summary>Creates the account.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the user list on success; the page with errors otherwise.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await identity.CreateUserAsync(
            new CreateUserCommand(Input.Email.Trim(), Input.DisplayName.Trim(), Input.Password, Input.Role),
            cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return Page();
        }

        TempData[StatusMessage.Success] = $"Created {Input.Email} as {Input.Role}.";
        return RedirectToPage("Index");
    }

    /// <summary>New-user form fields.</summary>
    public sealed class InputModel
    {
        /// <summary>Sign-in email.</summary>
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        /// <summary>Friendly name.</summary>
        [Required, StringLength(100)]
        [Display(Name = "Display name")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Initial password (Identity policy: 12+ characters, upper, lower, digit).</summary>
        [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 12)]
        public string Password { get; set; } = string.Empty;

        /// <summary>Assigned role.</summary>
        [Required]
        public string Role { get; set; } = AppRoles.Viewer;
    }
}
