using System.Globalization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Domain.Reporting;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Settlements;

/// <summary>
/// The ungrouped settlement lines (from <c>rpt.vw_SettlementLine</c>) behind one summary row, with
/// plusses and minuses in the Sales/Fees/Refunds/Other columns, so they explain the summary's totals.
/// </summary>
/// <param name="reporting">Reporting queries.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class LinesModel(IReportingQueries reporting, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>The SKU of the summary row; blank selects the row of amounts that belong to no SKU.</summary>
    [BindProperty(SupportsGet = true, Name = "sku")]
    public string? Sku { get; set; }

    /// <summary>The sale price of the summary row, invariant-culture text; blank selects the row without one.</summary>
    [BindProperty(SupportsGet = true, Name = "price")]
    public string? Price { get; set; }

    /// <summary>The summary row being explained (its totals are the footer to match).</summary>
    public SettlementSkuSummary Group { get; private set; } = default!;

    /// <summary>The current page of lines.</summary>
    public PagedResult<SettlementLineDetail> Lines { get; private set; } = default!;

    /// <summary>Loads the summary row and the requested page of its lines.</summary>
    /// <param name="id">Settlement id from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or 404 when the row does not exist in the current marketplace.</returns>
    public async Task<IActionResult> OnGetAsync(string id, CancellationToken cancellationToken)
    {
        var sku = string.IsNullOrWhiteSpace(Sku) ? null : Sku;
        decimal? price = null;
        if (!string.IsNullOrWhiteSpace(Price))
        {
            if (!decimal.TryParse(Price, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return NotFound();
            }

            price = parsed;
        }

        var marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var group = await reporting.GetSettlementSkuGroupAsync(marketplace.MarketplaceId, id, sku, price, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        Group = group;
        Lines = await reporting.GetSettlementLinesAsync(marketplace.MarketplaceId, id, sku, price, ToPageRequest(), cancellationToken);
        return Page();
    }

    /// <summary>The filters to keep when paging.</summary>
    /// <returns>The row's SKU and price, as they appeared in the request.</returns>
    public IReadOnlyDictionary<string, string> PagerFilters() => new Dictionary<string, string>
    {
        ["sku"] = Sku ?? string.Empty,
        ["price"] = Price ?? string.Empty,
    };
}
