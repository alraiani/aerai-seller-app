using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.UI.Models;

namespace AERai.Web.UI.Pages.Inventory;

/// <summary>
/// Each SKU's stock in the selected marketplace, broken down by state, with recent sales and days
/// of inventory, most urgent first.
/// </summary>
/// <param name="inventory">Inventory service.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class IndexModel(IInventoryService inventory, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>The marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>Totals and the current page of SKUs.</summary>
    public InventoryOverview Overview { get; private set; } = default!;

    /// <summary>Loads the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Overview = await inventory.GetOverviewAsync(Marketplace, ToPageRequest(), cancellationToken);
    }
}
