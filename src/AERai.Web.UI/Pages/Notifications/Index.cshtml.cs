using AERai.Web.Application.Alerts;
using AERai.Web.Application.Marketplaces;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Notifications;

/// <summary>
/// Low and out-of-stock alerts for the selected marketplace: open ones first, then those resolved
/// in the last week. Read state is per user.
/// </summary>
/// <param name="alerts">Alert service.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class IndexModel(IStockAlertService alerts, ICurrentMarketplace currentMarketplace) : PageModel
{
    /// <summary>The marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>The alerts.</summary>
    public IReadOnlyList<StockAlertView> Alerts { get; private set; } = [];

    /// <summary>Loads the alerts.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Alerts = await alerts.ListAsync(Marketplace.MarketplaceId, User.Identity!.Name!, cancellationToken);
    }

    /// <summary>Marks one alert read.</summary>
    /// <param name="id">Alert id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostReadAsync(long id, CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        await alerts.MarkReadAsync(Marketplace.MarketplaceId, id, User.Identity!.Name!, cancellationToken);
        return RedirectToPage();
    }

    /// <summary>Marks every open alert read.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostReadAllAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var count = await alerts.MarkAllReadAsync(Marketplace.MarketplaceId, User.Identity!.Name!, cancellationToken);
        TempData[StatusMessage.Success] = count == 0 ? "Nothing new to mark." : $"Marked {count:N0} alert{(count == 1 ? "" : "s")} read.";
        return RedirectToPage();
    }
}
