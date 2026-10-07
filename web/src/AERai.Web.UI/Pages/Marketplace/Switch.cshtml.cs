using AERai.Web.Application.Marketplaces;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Marketplace;

/// <summary>Handles the top-bar marketplace switcher: remembers the choice, then returns to the page.</summary>
/// <param name="currentMarketplace">Current-marketplace service.</param>
public sealed class SwitchModel(ICurrentMarketplace currentMarketplace) : PageModel
{
    /// <summary>Nothing to show; send stray GETs to the dashboard.</summary>
    /// <returns>A redirect.</returns>
    public IActionResult OnGet() => RedirectToPage("/Index");

    /// <summary>Switches marketplace and redirects back to the page the user was on.</summary>
    /// <param name="marketplaceId">Marketplace to switch to.</param>
    /// <param name="returnUrl">Local URL to return to.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect.</returns>
    public async Task<IActionResult> OnPostAsync(string marketplaceId, string? returnUrl, CancellationToken cancellationToken)
    {
        var result = await currentMarketplace.SelectAsync(marketplaceId ?? string.Empty, cancellationToken);
        if (result.IsFailure)
        {
            TempData[StatusMessage.Error] = result.Error;
        }

        // Only ever return to a page of this app (open-redirect guard). The page number is dropped
        // because page 3 of one marketplace's list means nothing in another.
        var target = Url.IsLocalUrl(returnUrl) ? StripPaging(returnUrl!) : Url.Page("/Index")!;
        return LocalRedirect(target);
    }

    private static string StripPaging(string url)
    {
        var queryStart = url.IndexOf('?', StringComparison.Ordinal);
        if (queryStart < 0)
        {
            return url;
        }

        var kept = url[(queryStart + 1)..]
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair => !pair.StartsWith("p=", StringComparison.Ordinal));
        var query = string.Join('&', kept);
        return query.Length == 0 ? url[..queryStart] : $"{url[..queryStart]}?{query}";
    }
}
