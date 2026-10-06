using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Security;
using AERai.Web.Domain.Core;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Inventory;

/// <summary>
/// Each SKU's stock in the selected marketplace: an Overview (status, stock, cover, sales, next
/// restock step) or a Stock breakdown (every Amazon state plus home stock). Filterable by search,
/// family, and status; sortable; most urgent first by default.
/// </summary>
/// <param name="inventory">Inventory service.</param>
/// <param name="items">Item editing service (for the family list).</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class IndexModel(IInventoryService inventory, IInventoryItemService items, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>Rows per page; inventory is scanned, so pages are longer than other lists.</summary>
    public const int PageSize = 50;

    /// <summary>Family filter from <c>?family=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "family")]
    public int? FamilyId { get; set; }

    /// <summary>Status filter from <c>?status=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "status")]
    public StockStatusFilter Status { get; set; }

    /// <summary>Sort from <c>?sort=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public InventorySort Sort { get; set; }

    /// <summary>Reverse the sort, from <c>?desc=true</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "desc")]
    public bool Descending { get; set; }

    /// <summary>Which columns to show, from <c>?view=breakdown</c> (default: overview).</summary>
    [BindProperty(SupportsGet = true, Name = "view")]
    public string? View { get; set; }

    /// <summary>Whether the Stock breakdown view is shown.</summary>
    public bool IsBreakdown => string.Equals(View, "breakdown", StringComparison.OrdinalIgnoreCase);

    /// <summary>The marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>Totals and the current page of SKUs.</summary>
    public InventoryOverview Overview { get; private set; } = default!;

    /// <summary>Families for the filter.</summary>
    public IReadOnlyList<ProductFamily> Families { get; private set; } = [];

    /// <summary>Whether the user may edit items and home stock.</summary>
    public bool CanEdit => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Operator);

    /// <summary>Whether any filter (not the view or sort) is applied.</summary>
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || FamilyId is not null || Status != StockStatusFilter.All;

    /// <summary>Filters, sort, and view to keep when paging.</summary>
    public IReadOnlyDictionary<string, string> PagerFilters =>
        Request.Query.Where(q => q.Key is not ("p" or "q")).ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Loads the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Families = await items.ListFamiliesAsync(cancellationToken);
        Overview = await inventory.GetOverviewAsync(
            Marketplace,
            new Application.Common.PageRequest(PageNumber, PageSize, Search),
            new InventoryFilter(FamilyId, Status, Sort, Descending),
            cancellationToken);
    }

    /// <summary>
    /// Builds a link to this page with the current filters, changing some of them. A <see langword="null"/>
    /// value removes that parameter (back to its default). Paging always restarts at page 1.
    /// </summary>
    /// <param name="changes">Query parameters to set or clear.</param>
    /// <returns>A relative URL.</returns>
    public string Link(params (string Key, string? Value)[] changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var values = Request.Query.Where(q => q.Key != "p").ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in changes)
        {
            if (string.IsNullOrEmpty(value))
            {
                values.Remove(key);
            }
            else
            {
                values[key] = value;
            }
        }

        return Url.Page("Index") + new QueryBuilder(values.Where(v => !string.IsNullOrEmpty(v.Value))).ToQueryString();
    }

    /// <summary>Link for a sortable column header: toggles direction when it's already the sort.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>A relative URL.</returns>
    public string SortLink(InventorySort column) =>
        Link(("sort", column == InventorySort.Urgency ? null : column.ToString()), ("desc", Sort == column && !Descending ? "true" : null));

    /// <summary>Arrow for a sortable column header.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>"↑", "↓", or empty.</returns>
    public string SortArrow(InventorySort column) => Sort != column ? string.Empty : Descending ? "↓" : "↑";

    /// <summary>Link to a status filter (tiles and pills), keeping search, family, and view.</summary>
    /// <param name="status">Status group.</param>
    /// <returns>A relative URL.</returns>
    public string StatusLink(StockStatusFilter status) => Link(("status", status == StockStatusFilter.All ? null : status.ToString()));
}
