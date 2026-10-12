using AERai.Web.Application.Inventory;
using AERai.Web.Application.Security;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Inventory;

/// <summary>
/// Creates, renames, and deletes product families, and puts selected SKUs in a family. The Inventory
/// page shows the same controls in a dialog and posts here; every action returns to the list the
/// user was looking at. Operators and Admins only.
/// </summary>
/// <param name="families">Family use cases.</param>
[Authorize(Policy = AppPolicies.RequireOperator)]
public sealed class FamiliesModel(IProductFamilyService families) : PageModel
{
    /// <summary>Families with their SKU counts (for the page view).</summary>
    public IReadOnlyList<FamilySummary> Families { get; private set; } = [];

    /// <summary>The list's query string to return to.</summary>
    [BindProperty(SupportsGet = true, Name = "back")]
    public string? Back { get; set; }

    /// <summary>Shows the family manager as a page (used when the dialog can't open, e.g. without JavaScript).</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Families = await families.ListAsync(cancellationToken);

    /// <summary>Creates a family.</summary>
    /// <param name="name">Name as typed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostCreateAsync(string? name, CancellationToken cancellationToken)
    {
        var result = await families.CreateAsync(name, cancellationToken);
        Report(result.IsSuccess, result.IsSuccess ? $"Family \"{result.Value}\" created. Tick SKUs in the list and use Set family to fill it." : result.Error);
        return BackToList(openDialog: true);
    }

    /// <summary>Renames a family.</summary>
    /// <param name="id">Family id.</param>
    /// <param name="name">New name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostRenameAsync(int id, string? name, CancellationToken cancellationToken)
    {
        var result = await families.RenameAsync(id, name, cancellationToken);
        Report(result.IsSuccess, result.IsSuccess ? $"Renamed to \"{result.Value}\"." : result.Error);
        return BackToList(openDialog: true);
    }

    /// <summary>Deletes a family; its SKUs become unassigned.</summary>
    /// <param name="id">Family id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        var result = await families.DeleteAsync(id, cancellationToken);
        Report(result.IsSuccess, result.IsSuccess ? $"Family \"{result.Value}\" deleted. Its SKUs are no longer in a family." : result.Error);

        // The list may have been filtered to the deleted family; drop that filter so it isn't empty.
        return BackToList(openDialog: true, dropFamilyFilter: true);
    }

    /// <summary>Puts the ticked SKUs in a family (created if new), or clears their family.</summary>
    /// <param name="skus">Ticked SKUs.</param>
    /// <param name="family">Family name as typed.</param>
    /// <param name="clear">True when "Remove from family" was clicked.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostAssignAsync(string[]? skus, string? family, bool clear, CancellationToken cancellationToken)
    {
        if (!clear && string.IsNullOrWhiteSpace(family))
        {
            Report(false, "Type or pick a family to put the selected SKUs in.");
            return BackToList(openDialog: false);
        }

        var result = await families.AssignAsync(skus ?? [], clear ? null : family, cancellationToken);
        var noun = result.IsSuccess && result.Value == 1 ? "SKU" : "SKUs";
        Report(result.IsSuccess, !result.IsSuccess ? result.Error
            : clear ? $"Removed {result.Value} {noun} from {(result.Value == 1 ? "its" : "their")} family."
            : $"Put {result.Value} {noun} in \"{FamilyNames.Normalize(family)}\".");
        return BackToList(openDialog: false);
    }

    private void Report(bool success, string message) =>
        TempData[success ? StatusMessage.Success : StatusMessage.Error] = message;

    /// <summary>Redirects to the Inventory list with the filters the user was looking at.</summary>
    private RedirectResult BackToList(bool openDialog, bool dropFamilyFilter = false)
    {
        // Only a query string is accepted, so this can never redirect off-site.
        var query = Back is { Length: > 1 } && Back[0] == '?' ? Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(Back) : [];
        var values = query
            .Where(q => q.Key is not ("p" or "families") && !(dropFamilyFilter && q.Key == "family"))
            .ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        if (openDialog)
        {
            // Reopens the Families dialog so several families can be managed in a row.
            values["families"] = "open";
        }

        return Redirect(Url.Page("Index") + new Microsoft.AspNetCore.Http.Extensions.QueryBuilder(values).ToQueryString());
    }
}
