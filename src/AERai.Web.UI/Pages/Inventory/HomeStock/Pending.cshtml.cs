using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Security;
using AERai.Web.Domain.Core;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AERai.Web.UI.Pages.Inventory.HomeStock;

/// <summary>
/// Sends from home stock that Amazon's numbers don't show yet, across the marketplace or one family.
/// Operators and Admins add the FBA shipment id to a send logged without one, mark a send reconciled, or
/// cancel one that never went out; the Worksheet's panel posts its actions here too.
/// </summary>
/// <param name="pending">Pending sends.</param>
/// <param name="items">Families for the filter.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
/// <param name="options">Restock settings (when a pending send is overdue).</param>
public sealed class PendingModel(IPendingSendService pending, IInventoryItemService items, ICurrentMarketplace currentMarketplace, IOptions<InventoryOptions> options) : PageModel
{
    /// <summary>Marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>Families for the filter.</summary>
    public IReadOnlyList<ProductFamily> Families { get; private set; } = [];

    /// <summary>Only this family's sends, from <c>?family=</c>; all when absent.</summary>
    [BindProperty(SupportsGet = true, Name = "family")]
    public int? FamilyId { get; set; }

    /// <summary>The open sends.</summary>
    public PendingSendsPanel Panel { get; private set; } = default!;

    /// <summary>Whether the user can change pending sends.</summary>
    public bool CanEdit => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Operator);

    /// <summary>Lists the open sends.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Families = await items.ListFamiliesAsync(cancellationToken);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Marketplace.TimeZoneId);
        var list = await pending.ListAsync(Marketplace.MarketplaceId, FamilyId, cancellationToken);
        Panel = new PendingSendsPanel(list, CanEdit, zone, options.Value.PendingSendExpiryDays, null, FamilyId);
    }

    /// <summary>Adds the FBA shipment id to a send logged without one.</summary>
    /// <param name="id">The send's ledger entry id.</param>
    /// <param name="shipmentId">The FBA shipment id.</param>
    /// <param name="back"><c>worksheet</c> to return to the Worksheet; otherwise this page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back.</returns>
    public Task<IActionResult> OnPostShipmentIdAsync(long id, string? shipmentId, string? back, CancellationToken cancellationToken) =>
        ActAsync(back, (m, user) => pending.SetShipmentIdAsync(m, id, shipmentId, user, cancellationToken), "Shipment id added; an inventory sync is on its way to check it.", cancellationToken);

    /// <summary>Adds one FBA shipment id to every ticked send logged without one.</summary>
    /// <param name="ids">The ticked sends' ledger entry ids.</param>
    /// <param name="shipmentId">The FBA shipment id.</param>
    /// <param name="back"><c>worksheet</c> to return to the Worksheet; otherwise this page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back.</returns>
    public async Task<IActionResult> OnPostShipmentIdsAsync(long[]? ids, string? shipmentId, string? back, CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            return Forbid();
        }

        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var result = await pending.SetShipmentIdsAsync(Marketplace.MarketplaceId, ids ?? [], shipmentId, User.Identity!.Name!, cancellationToken); // Non-null: the page requires an authenticated user.
        if (result.IsFailure)
        {
            TempData[StatusMessage.Error] = result.Error;
        }
        else
        {
            var (added, skipped) = result.Value;
            TempData[StatusMessage.Success] = $"Shipment id added to {added:N0} send{(added == 1 ? "" : "s")}; an inventory sync is on its way to check {(added == 1 ? "it" : "them")}."
                + (skipped > 0 ? $" {skipped:N0} skipped: no longer pending or already had a reference." : string.Empty);
        }

        return Back(back);
    }

    /// <summary>Closes a send by hand.</summary>
    /// <param name="id">The send's ledger entry id.</param>
    /// <param name="back"><c>worksheet</c> to return to the Worksheet; otherwise this page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back.</returns>
    public Task<IActionResult> OnPostReconciledAsync(long id, string? back, CancellationToken cancellationToken) =>
        ActAsync(back, (m, user) => pending.MarkReconciledAsync(m, id, user, cancellationToken), "Marked as received by Amazon; it no longer counts as pending.", cancellationToken);

    /// <summary>Cancels a send that never went out (its units return to home stock).</summary>
    /// <param name="id">The send's ledger entry id.</param>
    /// <param name="back"><c>worksheet</c> to return to the Worksheet; otherwise this page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back.</returns>
    public Task<IActionResult> OnPostCancelAsync(long id, string? back, CancellationToken cancellationToken) =>
        ActAsync(back, (m, user) => pending.CancelAsync(m, id, user, cancellationToken), "Send cancelled; its units are back in home stock (see the ledger).", cancellationToken);

    private async Task<IActionResult> ActAsync(string? back, Func<string, string, Task<Application.Common.Result>> action, string success, CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            return Forbid();
        }

        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var result = await action(Marketplace.MarketplaceId, User.Identity!.Name!); // Non-null: the page requires an authenticated user.
        TempData[result.IsSuccess ? StatusMessage.Success : StatusMessage.Error] = result.IsSuccess ? success : result.Error;
        return Back(back);
    }

    private RedirectToPageResult Back(string? back) => back == "worksheet"
        ? RedirectToPage("/Inventory/Worksheet", new { family = FamilyId })
        : RedirectToPage(new { family = FamilyId });
}
