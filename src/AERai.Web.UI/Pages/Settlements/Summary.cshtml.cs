using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Reporting;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Settlements;

/// <summary>
/// One settlement broken down by SKU, sale price, and currency (from <c>rpt.vw_SettlementSkuSummary</c>);
/// each row drills down to the orders behind it. Searchable by SKU, ASIN, or title; the rows can be
/// narrowed to those with refunds or to the amounts without a SKU, and sorted by any amount.
/// </summary>
/// <param name="reporting">Reporting queries.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class SummaryModel(IReportingQueries reporting, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>Which rows, from <c>?kind=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "kind")]
    public SettlementRowKind Kind { get; set; }

    /// <summary>Sort from <c>?sort=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public SettlementSkuSort Sort { get; set; }

    /// <summary>Reverse the sort, from <c>?desc=true</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "desc")]
    public bool Descending { get; set; }

    /// <summary>The settlement's totals.</summary>
    public SettlementSummary Settlement { get; private set; } = default!;

    /// <summary>The current page of summary rows.</summary>
    public PagedResult<SettlementSkuSummary> Rows { get; private set; } = default!;

    /// <summary>Whether a search or row filter is applied (the footer is then the whole settlement, not the rows shown).</summary>
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || Kind != SettlementRowKind.All;

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
        Rows = await reporting.GetSettlementSkuSummaryAsync(
            marketplace.MarketplaceId, id, ToPageRequest(), new SettlementSkuFilter(Kind, Sort, Descending), cancellationToken);
        return Page();
    }

    /// <summary>Link for a sortable column header: toggles direction when it's already the sort.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>A relative URL.</returns>
    public string SortLink(SettlementSkuSort column) =>
        LinkWith(("sort", column == SettlementSkuSort.Sales ? null : column.ToString()), ("desc", Sort == column && !Descending ? "true" : null));

    /// <summary>Arrow for a sortable column header.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>"↑", "↓", or empty.</returns>
    public string SortArrow(SettlementSkuSort column) => SettlementFormat.Arrow(Sort == column, Sort.IsDescendingByDefault() != Descending);

    /// <summary>Link to a row filter, keeping the search and sort.</summary>
    /// <param name="kind">Which rows.</param>
    /// <returns>A relative URL.</returns>
    public string KindLink(SettlementRowKind kind) => LinkWith(("kind", kind == SettlementRowKind.All ? null : kind.ToString()));
}
