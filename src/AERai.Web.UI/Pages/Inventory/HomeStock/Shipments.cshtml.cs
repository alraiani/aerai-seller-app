using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Security;
using AERai.Web.Domain.Core;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Inventory.HomeStock;

/// <summary>
/// The shipments the seller created in Send to Amazon, read by the Amazon shipments sync. Operators and Admins accept
/// one (its units from home stock are logged as a Shipped to Amazon send under its FBA shipment id) or dismiss it;
/// everyone can view.
/// </summary>
/// <param name="shipments">Shipment review.</param>
/// <param name="items">Families for the filter.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class ShipmentsModel(IInboundShipmentService shipments, IInventoryItemService items, ICurrentMarketplace currentMarketplace) : PageModel
{
    /// <summary>Marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>Families for the filter.</summary>
    public IReadOnlyList<ProductFamily> Families { get; private set; } = [];

    /// <summary>Only shipments holding this family's SKUs, from <c>?family=</c>; all when absent.</summary>
    [BindProperty(SupportsGet = true, Name = "family")]
    public int? FamilyId { get; set; }

    /// <summary>Search text (FBA id, plan, SKU, or title), from <c>?q=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    /// <summary>Which shipments to show, from <c>?view=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "view")]
    public InboundShipmentView View { get; set; }

    /// <summary>The shipments and the counts for the view switch.</summary>
    public InboundShipmentList List { get; private set; } = default!;

    /// <summary>The marketplace's time zone, for plan dates.</summary>
    public TimeZoneInfo Zone { get; private set; } = TimeZoneInfo.Utc;

    /// <summary>Whether a search or family narrows the list.</summary>
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || FamilyId is not null;

    /// <summary>Whether the user can accept and dismiss shipments.</summary>
    public bool CanEdit => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Operator);

    /// <summary>The view value for links and redirects (none for the default).</summary>
    public string? ViewParam => View == InboundShipmentView.ToReview ? null : View.ToString();

    /// <summary>Lists the shipments.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or just its section for a partial reload.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Zone = TimeZoneInfo.FindSystemTimeZoneById(Marketplace.TimeZoneId);
        Families = await items.ListFamiliesAsync(cancellationToken);
        List = await shipments.ListAsync(Marketplace.MarketplaceId, new InboundShipmentFilter(View, Search, FamilyId), cancellationToken);
        return this.PageOrFragment("_ShipmentsBody");
    }

    /// <summary>Logs a shipment's units from home stock as a send under its FBA shipment id.</summary>
    /// <param name="id">The shipment's id.</param>
    /// <param name="lines">Per SKU: the home stock shown and the units typed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostAcceptAsync(long id, AcceptLine[]? lines, CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData[StatusMessage.Error] = "Enter whole units, from 0 up.";
            return BackToList();
        }

        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var sends = (lines ?? [])
            .Where(l => !string.IsNullOrWhiteSpace(l.Sku))
            .Select(l => new HomeStockShipment(l.Sku!.Trim(), l.Current, l.Units ?? 0)) // Non-null: blank SKUs were filtered out just above.
            .ToList();
        var result = await shipments.AcceptAsync(Marketplace.MarketplaceId, id, sends, User.Identity!.Name!, cancellationToken); // Non-null: the page requires an authenticated user.
        if (result.IsFailure)
        {
            TempData[StatusMessage.Error] = result.Error;
            return BackToList();
        }

        var done = result.Value;
        if (done.Stale.Count > 0)
        {
            TempData[StatusMessage.Error] = $"Home stock of {string.Join(", ", done.Stale)} changed after the page loaded, so {(done.Stale.Count == 1 ? "it was" : "they were")} not logged; check the numbers and accept again."
                + (done.Applied > 0 ? $" {done.Applied:N0} other SKU{(done.Applied == 1 ? " was" : "s were")} logged ({done.UnitsOut:N0} units)." : string.Empty);
        }
        else
        {
            TempData[StatusMessage.Success] = done.Applied == 0
                ? "Every SKU from home was already logged under this shipment id; marked as logged."
                : $"Logged {done.UnitsOut:N0} units of {done.Applied:N0} SKU{(done.Applied == 1 ? "" : "s")} as shipped to Amazon. They show under Awaiting Amazon until Amazon’s numbers catch up.";
        }

        return BackToList();
    }

    /// <summary>Sets a shipment aside without touching home stock.</summary>
    /// <param name="id">The shipment's id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public Task<IActionResult> OnPostDismissAsync(long id, CancellationToken cancellationToken) =>
        ActAsync((m, user) => shipments.DismissAsync(m, id, user, cancellationToken), "Shipment dismissed; home stock is unchanged. Find it under Set aside.", cancellationToken);

    /// <summary>Puts a dismissed shipment back to review.</summary>
    /// <param name="id">The shipment's id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public Task<IActionResult> OnPostReopenAsync(long id, CancellationToken cancellationToken) =>
        ActAsync((m, user) => shipments.ReopenAsync(m, id, user, cancellationToken), "Shipment is back to review.", cancellationToken);

    private async Task<IActionResult> ActAsync(Func<string, string, Task<Application.Common.Result>> action, string success, CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            return Forbid();
        }

        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var result = await action(Marketplace.MarketplaceId, User.Identity!.Name!); // Non-null: the page requires an authenticated user.
        TempData[result.IsSuccess ? StatusMessage.Success : StatusMessage.Error] = result.IsSuccess ? success : result.Error;
        return BackToList();
    }

    private RedirectToPageResult BackToList() => RedirectToPage(new { family = FamilyId, q = Search, view = ViewParam });

    /// <summary>One SKU of an accept form.</summary>
    public sealed class AcceptLine
    {
        /// <summary>Seller SKU.</summary>
        public string? Sku { get; set; }

        /// <summary>Home stock shown when the page loaded; the SKU is skipped if it has changed since.</summary>
        public int Current { get; set; }

        /// <summary>Units typed (blank = 0, not logged).</summary>
        [System.ComponentModel.DataAnnotations.Range(0, 1_000_000)]
        public int? Units { get; set; }
    }
}
