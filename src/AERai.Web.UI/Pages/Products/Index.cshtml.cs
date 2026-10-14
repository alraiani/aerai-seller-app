using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Products;
using AERai.Web.Application.Security;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Products;

/// <summary>
/// Product master data and cost of goods: tiles by whether a cost is set, search (every word must
/// match), family filter, sortable columns, and — for Operators — costs edited in place and saved
/// together, plus the downloadable cost sheet and its upload.
/// </summary>
/// <param name="products">Product queries.</param>
/// <param name="productService">Product use cases (saves costs).</param>
/// <param name="families">Families for the filter.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class IndexModel(
    IProductRepository products,
    IProductService productService,
    IProductFamilyService families,
    ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>Products listed per page.</summary>
    public const int PageSize = 50;

    /// <summary>The current page of products.</summary>
    public PagedResult<ProductSummary> Products { get; private set; } = default!;

    /// <summary>Counts for the tiles.</summary>
    public ProductListTotals Totals { get; private set; } = new(0, 0);

    /// <summary>Families for the filter.</summary>
    public IReadOnlyList<FamilySummary> Families { get; private set; } = [];

    /// <summary>Marketplace whose costs are listed (costs are in its currency).</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>Family filter from <c>?family=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "family")]
    public int? FamilyId { get; set; }

    /// <summary>Cost filter from <c>?cost=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "cost")]
    public ProductCostFilter Cost { get; set; }

    /// <summary>Sort from <c>?sort=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public ProductSort Sort { get; set; }

    /// <summary>Reverse the sort, from <c>?desc=true</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "desc")]
    public bool Descending { get; set; }

    /// <summary>The inline cost boxes, as posted.</summary>
    [BindProperty]
    public List<CostInput> Rows { get; set; } = [];

    /// <summary>Per-SKU problems with the posted costs, shown under the box.</summary>
    public Dictionary<string, string> RowErrors { get; } = new(StringComparer.Ordinal);

    /// <summary>Whether the user may edit costs (inline boxes, sheet, upload).</summary>
    public bool CanEdit => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Operator);

    /// <summary>Whether a search or family filter is applied (the cost filter is a tile, not a filter to clear).</summary>
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || FamilyId is not null;

    /// <summary>The currency symbol shown in front of each cost box.</summary>
    public string Symbol => DashboardFormat.Symbol(Marketplace.Currency);

    /// <summary>Loads the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    /// <summary>
    /// Saves the cost boxes that changed. Every box is checked first; if any can't be read nothing is
    /// saved and the page shows the typed values with the problems. A SKU whose cost someone else
    /// changed since the page loaded is skipped and named.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the same list, or the page with errors.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Checked here rather than with the Operator policy on the page, because the list itself
        // (GET) stays open to Viewers.
        if (!CanEdit)
        {
            return Forbid();
        }

        var changes = new List<ProductCostChange>();
        foreach (var row in Rows.Where(r => !string.IsNullOrWhiteSpace(r.Sku)))
        {
            if (!ProductCostText.TryParse(row.Original, out var original))
            {
                continue;
            }

            if (!ProductCostText.TryParse(row.Value, out var value))
            {
                RowErrors[row.Sku] = $"Enter a cost from 0 to {ProductService.MaxCostOfGoods:N0}, or leave it blank.";
                continue;
            }

            if (value != original)
            {
                changes.Add(new ProductCostChange(row.Sku, original, value));
            }
        }

        if (RowErrors.Count > 0)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var result = await productService.SaveCostsAsync(Marketplace.MarketplaceId, changes, cancellationToken);
        if (result.IsFailure)
        {
            TempData[StatusMessage.Error] = result.Error;
        }
        else
        {
            var r = result.Value;
            TempData[StatusMessage.Success] = r.Applied == 0 && r.Stale.Count == 0
                ? "No costs changed."
                : $"Saved {r.Applied:N0} {Marketplace.Code} cost{(r.Applied == 1 ? "" : "s")} of goods.";
            if (r.Stale.Count > 0)
            {
                TempData[StatusMessage.Error] =
                    $"Skipped {r.Stale.Count:N0} SKU{(r.Stale.Count == 1 ? "" : "s")} whose cost changed after this page loaded: {string.Join(", ", r.Stale.Take(10))}{(r.Stale.Count > 10 ? ", …" : "")}. Check them and save again.";
            }
        }

        return LocalRedirect(Request.PathBase + Request.Path + Request.QueryString);
    }

    /// <summary>The value to show in a SKU's cost box: what was posted when redisplaying errors, else the stored cost.</summary>
    /// <param name="product">The product.</param>
    /// <returns>Cost text ("4.50"), or empty when not set.</returns>
    public string ValueFor(ProductSummary product)
    {
        ArgumentNullException.ThrowIfNull(product);
        return Rows.FirstOrDefault(r => r.Sku == product.Sku) is { } posted && RowErrors.Count > 0
            ? posted.Value ?? string.Empty
            : ProductCostText.Format(product.CostOfGoods);
    }

    /// <summary>Link for a cost tile: shows that group (or everything), keeping search, family, and sort.</summary>
    /// <param name="cost">Cost group.</param>
    /// <returns>A relative URL.</returns>
    public string CostLink(ProductCostFilter cost) => LinkWith(("cost", cost == ProductCostFilter.All ? null : cost.ToString()));

    /// <summary>Link for a sortable column header: toggles direction when it's already the sort.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>A relative URL.</returns>
    public string SortLink(ProductSort column) =>
        LinkWith(("sort", column == ProductSort.Sku ? null : column.ToString()), ("desc", Sort == column && !Descending ? "true" : null));

    /// <summary>The <c>aria-sort</c> value for a column header, so screen readers announce the order.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>"ascending", "descending", or "none".</returns>
    public string AriaSort(ProductSort column) =>
        Sort != column ? "none" : column.IsDescendingByDefault() != Descending ? "descending" : "ascending";

    /// <summary>Arrow for a sortable column header, matching the order shown.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>"↑", "↓", or empty.</returns>
    public string SortArrow(ProductSort column) => AriaSort(column) switch
    {
        "ascending" => "↑",
        "descending" => "↓",
        _ => string.Empty,
    };

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Families = await families.ListAsync(cancellationToken);
        var request = new PageRequest(PageNumber, PageSize, Search);
        Products = await products.ListAsync(Marketplace.MarketplaceId, new ProductListFilter(Cost, FamilyId, Sort, Descending), request, cancellationToken);
        Totals = await products.GetTotalsAsync(Marketplace.MarketplaceId, FamilyId, Search, cancellationToken);
    }

    /// <summary>One cost box on the list.</summary>
    public sealed class CostInput
    {
        /// <summary>Seller SKU.</summary>
        public string Sku { get; set; } = string.Empty;

        /// <summary>The cost when the page loaded ("4.50", or empty when not set); a save is skipped if it no longer matches.</summary>
        public string? Original { get; set; }

        /// <summary>The cost typed in the box; blank clears it.</summary>
        public string? Value { get; set; }
    }
}
