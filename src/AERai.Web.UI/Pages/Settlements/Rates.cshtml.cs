using System.Globalization;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Security;
using AERai.Web.Application.Settlements;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Settlements;

/// <summary>
/// The exchange rate actions of the Settlements pages: fetch the missing market rates, and set or reset a
/// settlement's rate so the converted payout matches the bank. The pages show the forms and post here;
/// every action returns to the page it came from. Operators and Admins only.
/// </summary>
/// <param name="rates">Settlement exchange rate use cases.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
[Authorize(Policy = AppPolicies.RequireOperator)]
public sealed class RatesModel(ISettlementExchangeRateService rates, ICurrentMarketplace currentMarketplace) : PageModel
{
    /// <summary>The Settlements list's query string to return to after fetching.</summary>
    [BindProperty(SupportsGet = true, Name = "back")]
    public string? Back { get; set; }

    /// <summary>Nothing to show on its own; goes to the Settlements list.</summary>
    /// <returns>A redirect.</returns>
    public IActionResult OnGet() => RedirectToPage("Index");

    /// <summary>Fetches the market rate of every settlement still missing one.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostFetchAsync(CancellationToken cancellationToken)
    {
        var result = await rates.FillMissingAsync(cancellationToken);
        Report(result.Unavailable == 0,
            (result.Filled, result.Unavailable) switch
            {
                (0, 0) => "Every settlement that can be converted already has a rate (one paid out today or later gets its rate once the day's rate is published).",
                (_, 0) => $"Fetched {Plural(result.Filled)}.",
                (0, _) => $"No rate was available for {Plural(result.Unavailable)}; try again later, or enter the rate on the settlement's page.",
                _ => $"Fetched {Plural(result.Filled)}; none was available for {result.Unavailable.ToString("N0", CultureInfo.InvariantCulture)} more (enter those on the settlement's page).",
            });

        // Only a query string is accepted, so this can never redirect off-site.
        var query = Back is { Length: > 1 } && Back[0] == '?' ? Back : string.Empty;
        return Redirect(Url.Page("Index") + query);
    }

    /// <summary>Overrides a settlement's rate with the amount received or a rate.</summary>
    /// <param name="id">Settlement id.</param>
    /// <param name="received">Amount that reached the bank, in the marketplace's currency.</param>
    /// <param name="rate">Exchange rate.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the settlement.</returns>
    public async Task<IActionResult> OnPostSetAsync(string id, decimal? received, decimal? rate, CancellationToken cancellationToken)
    {
        var marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var result = await rates.SetManualAsync(marketplace.MarketplaceId, id, received, rate, User.Identity!.Name!, cancellationToken); // Non-null: the page requires an authenticated user.
        Report(result.IsSuccess, result.IsSuccess ? "Exchange rate saved; the settlement's converted amounts now use it." : result.Error);
        return RedirectToPage("Summary", new { id });
    }

    /// <summary>Removes a settlement's adjusted rate, so the market rate applies again.</summary>
    /// <param name="id">Settlement id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the settlement.</returns>
    public async Task<IActionResult> OnPostResetAsync(string id, CancellationToken cancellationToken)
    {
        var marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var result = await rates.ResetAsync(marketplace.MarketplaceId, id, cancellationToken);
        Report(result.IsSuccess, result.IsSuccess ? "Back to the market rate." : result.Error);
        return RedirectToPage("Summary", new { id });
    }

    private static string Plural(int count) =>
        $"{count.ToString("N0", CultureInfo.InvariantCulture)} settlement rate{(count == 1 ? "" : "s")}";

    private void Report(bool success, string message) =>
        TempData[success ? StatusMessage.Success : StatusMessage.Error] = message;
}
