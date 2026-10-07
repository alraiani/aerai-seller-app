using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Domain.Reporting;
using AERai.Web.UI.Models;

namespace AERai.Web.UI.Pages.Inventory;

/// <summary>
/// Current inventory position per SKU (from <c>rpt.vw_InventoryPosition</c>), most urgent first.
/// </summary>
/// <param name="reporting">Reporting queries.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class IndexModel(IReportingQueries reporting, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>The current page of positions.</summary>
    public PagedResult<InventoryPosition> Positions { get; private set; } = default!;

    /// <summary>Loads the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Positions = await reporting.GetInventoryPositionsAsync(marketplace.MarketplaceId, ToPageRequest(), cancellationToken);
    }
}
