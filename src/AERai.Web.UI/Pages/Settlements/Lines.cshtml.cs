using System.Globalization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Reporting;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Settlements;

/// <summary>
/// The settlement lines (from <c>rpt.vw_SettlementLine</c>) behind one summary row, grouped by order:
/// a totals row that matches the summary row, then one collapsible row per order (lines without an
/// order grouped by transaction type) that expands into its lines, each with one signed amount.
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

    /// <summary>Only groups with an amount in this column, from <c>?bucket=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "bucket")]
    public SettlementBucket? Bucket { get; set; }

    /// <summary>Sort from <c>?sort=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public SettlementGroupSort Sort { get; set; }

    /// <summary>Reverse the sort, from <c>?desc=true</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "desc")]
    public bool Descending { get; set; }

    /// <summary>The summary row being explained (its totals are the totals row).</summary>
    public SettlementSkuSummary Group { get; private set; } = default!;

    /// <summary>The current page of order groups.</summary>
    public PagedResult<SettlementOrderGroup> Groups { get; private set; } = default!;

    /// <summary>Whether a search or bucket filter is applied.</summary>
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || Bucket is not null;

    /// <summary>Loads the summary row and the requested page of its groups.</summary>
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
        Groups = await reporting.GetSettlementOrderGroupsAsync(
            marketplace.MarketplaceId, id, sku, price, ToPageRequest(), new SettlementLineFilter(Bucket, Sort, Descending), cancellationToken);
        return Page();
    }

    /// <summary>A link back to this summary row with only its SKU and price (no search, filter, or sort).</summary>
    /// <returns>A relative URL.</returns>
    public string ClearLink() => LinkWith(("q", null), ("bucket", null), ("sort", null), ("desc", null));

    /// <summary>Link for a sortable column header: toggles direction when it's already the sort.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>A relative URL.</returns>
    public string SortLink(SettlementGroupSort column) =>
        LinkWith(("sort", column == SettlementGroupSort.Posted ? null : column.ToString()), ("desc", Sort == column && !Descending ? "true" : null));

    /// <summary>Arrow for a sortable column header.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>"↑", "↓", or empty.</returns>
    public string SortArrow(SettlementGroupSort column) => SettlementFormat.Arrow(Sort == column, Sort.IsDescendingByDefault() != Descending);
}
