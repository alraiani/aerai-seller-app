using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Domain.Reporting;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Settlements;

/// <summary>
/// One settlement broken down by SKU, sale price, and currency (from <c>rpt.vw_SettlementSkuSummary</c>);
/// each row drills down to the lines behind it.
/// </summary>
/// <param name="reporting">Reporting queries.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class SummaryModel(IReportingQueries reporting, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>The settlement's totals.</summary>
    public SettlementSummary Settlement { get; private set; } = default!;

    /// <summary>The current page of summary rows.</summary>
    public PagedResult<SettlementSkuSummary> Rows { get; private set; } = default!;

    /// <summary>Loads the settlement and the requested page of rows.</summary>
    /// <param name="id">Settlement id from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or 404 when the settlement is not in the current marketplace.</returns>
    public async Task<IActionResult> OnGetAsync(string id, CancellationToken cancellationToken)
    {
        var marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var settlement = await reporting.GetSettlementAsync(marketplace.MarketplaceId, id, cancellationToken);
        if (settlement is null)
        {
            return NotFound();
        }

        Settlement = settlement;
        Rows = await reporting.GetSettlementSkuSummaryAsync(marketplace.MarketplaceId, id, ToPageRequest(), cancellationToken);
        return Page();
    }
}
