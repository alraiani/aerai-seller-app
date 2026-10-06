using System.Globalization;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Security;
using AERai.Web.Domain.Core;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AERai.Web.UI.Pages.Inventory;

/// <summary>
/// Each SKU's stock in the selected marketplace, broken down by state, with home stock, recent
/// sales, days of inventory, and what to send or order by when, most urgent first. Filterable by family.
/// </summary>
/// <param name="inventory">Inventory service.</param>
/// <param name="items">Item editing service (for the family list).</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
/// <param name="options">Restock settings (what counts as "soon").</param>
public sealed class IndexModel(IInventoryService inventory, IInventoryItemService items, ICurrentMarketplace currentMarketplace, IOptions<InventoryOptions> options) : ListPageModel
{
    /// <summary>Family filter from <c>?family=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "family")]
    public int? FamilyId { get; set; }

    /// <summary>The marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>Totals and the current page of SKUs.</summary>
    public InventoryOverview Overview { get; private set; } = default!;

    /// <summary>Families for the filter.</summary>
    public IReadOnlyList<ProductFamily> Families { get; private set; } = [];

    /// <summary>Restock actions due within this many days are highlighted.</summary>
    public int SoonDays => options.Value.AlertLeadDays;

    /// <summary>Whether the user may edit items and home stock.</summary>
    public bool CanEdit => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Operator);

    /// <summary>Filters to keep when paging.</summary>
    public IReadOnlyDictionary<string, string>? PagerFilters =>
        FamilyId is { } id ? new Dictionary<string, string> { ["family"] = id.ToString(CultureInfo.InvariantCulture) } : null;

    /// <summary>Loads the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Families = await items.ListFamiliesAsync(cancellationToken);
        Overview = await inventory.GetOverviewAsync(Marketplace, ToPageRequest(), FamilyId, InventorySort.Urgency, cancellationToken);
    }
}
