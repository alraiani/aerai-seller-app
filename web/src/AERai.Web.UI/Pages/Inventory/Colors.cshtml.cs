using AERai.Web.Application.Inventory;
using AERai.Web.Application.Security;
using AERai.Web.Domain.Core;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Inventory;

/// <summary>Sets the color of the SKUs ticked on the Inventory list. Operators and Admins only.</summary>
/// <param name="items">Item editing service.</param>
[Authorize(Policy = AppPolicies.RequireOperator)]
public sealed class ColorsModel(IInventoryItemService items) : PageModel
{
    /// <summary>The Inventory list's query string to return to (filters, page).</summary>
    [BindProperty(SupportsGet = true, Name = "back")]
    public string? Back { get; set; }

    /// <summary>Nothing to show; goes back to the list.</summary>
    /// <returns>A redirect.</returns>
    public IActionResult OnGet() => RedirectToPage("Index");

    /// <summary>Sets (or, with no color, clears) the ticked SKUs' color.</summary>
    /// <param name="skus">The ticked SKUs.</param>
    /// <param name="color">The color, or blank to clear.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostAssignAsync(string[]? skus, ProductColor? color, CancellationToken cancellationToken)
    {
        var result = await items.SetColorAsync(skus ?? [], color, cancellationToken);
        var noun = result.IsSuccess && result.Value == 1 ? "SKU" : "SKUs";
        TempData[result.IsSuccess ? StatusMessage.Success : StatusMessage.Error] = !result.IsSuccess ? result.Error
            : color is null ? $"Cleared the color of {result.Value} {noun}."
            : $"Set {result.Value} {noun} to {ColorFormat.Label(color)}.";

        // Only a query string is accepted, so this can never redirect off-site.
        var query = Back is { Length: > 1 } && Back[0] == '?' ? Back : string.Empty;
        return Redirect(Url.Page("Index") + query);
    }
}
