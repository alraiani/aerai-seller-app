using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Security;
using AERai.Web.Domain.Core;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AERai.Web.UI.Pages.Inventory;

/// <summary>
/// The inventory worksheet: one family's SKUs grouped and color-coded by color, with sales, Amazon
/// stock, what to send in and by when, home stock (editable in place), and the next supplier order.
/// Everyone can view it; Operators and Admins can edit home stock and log units sent to Amazon.
/// </summary>
/// <param name="inventory">Inventory numbers and the worksheet layout.</param>
/// <param name="items">Item editing (families list, home-stock saves).</param>
/// <param name="ledger">Home-stock ledger (logs units sent to Amazon).</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
/// <param name="options">Restock settings (when an action counts as due soon).</param>
/// <param name="clock">Clock (the sheet is dated in the marketplace's local calendar).</param>
public sealed class WorksheetModel(IInventoryService inventory, IInventoryItemService items, IHomeStockLedgerService ledger, ICurrentMarketplace currentMarketplace, IOptions<InventoryOptions> options, TimeProvider clock) : PageModel
{
    /// <summary>Marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>Today in the marketplace's time zone (printed on the sheet).</summary>
    public DateOnly Today { get; private set; }

    /// <summary>Every family, by name.</summary>
    public IReadOnlyList<ProductFamily> Families { get; private set; } = [];

    /// <summary>The family shown, from <c>?family=</c> (the first family when absent or unknown).</summary>
    [BindProperty(SupportsGet = true, Name = "family")]
    public int? FamilyId { get; set; }

    /// <summary>The selected family, or <see langword="null"/> when there are no families yet.</summary>
    public ProductFamily? Family { get; private set; }

    /// <summary>The worksheet, or <see langword="null"/> when there is no family to show.</summary>
    public InventoryWorksheet? Sheet { get; private set; }

    /// <summary>Restock actions due within this many days are highlighted.</summary>
    public int SoonDays => options.Value.AlertLeadDays;

    /// <summary>Whether the user can edit home stock.</summary>
    public bool CanEdit => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Operator);

    /// <summary>Posted home-stock counts and send amounts.</summary>
    [BindProperty]
    public List<RowInput> Rows { get; set; } = [];

    /// <summary>FBA shipment id recorded on every send, if given.</summary>
    [BindProperty]
    [StringLength(HomeStockLedgerService.MaxReferenceLength, ErrorMessage = "The shipment id can be at most 100 characters.")]
    public string? ShipmentReference { get; set; }

    /// <summary>The value for a SKU's home-stock box: what was typed when a save failed, otherwise the saved count.</summary>
    /// <param name="item">The SKU's row.</param>
    /// <returns>The input value.</returns>
    public string ValueFor(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var index = Rows.FindIndex(r => r.Sku == item.Sku);
        var key = $"{nameof(Rows)}[{index}].{nameof(RowInput.Quantity)}";
        return index >= 0 && ModelState.TryGetValue(key, out var entry) && entry.AttemptedValue is { } typed
            ? typed
            : item.Position.HomeStock.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The value for a SKU's send box: what was typed when a save failed, otherwise empty.</summary>
    /// <param name="item">The SKU's row.</param>
    /// <returns>The input value.</returns>
    public string SendValueFor(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var index = Rows.FindIndex(r => r.Sku == item.Sku);
        var key = $"{nameof(Rows)}[{index}].{nameof(RowInput.Send)}";
        return index >= 0 && ModelState.TryGetValue(key, out var entry) && entry.AttemptedValue is { } typed ? typed : string.Empty;
    }

    /// <summary>Shows the worksheet.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    /// <summary>
    /// Saves the sheet: changed home-stock counts are logged as count corrections, and send amounts as
    /// Shipped to Amazon (home stock goes down by the amount sent).
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the worksheet, or the page with errors.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            return Forbid();
        }

        // A recount and a send on the same row would be ambiguous (send from the old count or the new one?).
        foreach (var both in Rows.Where(r => r.Quantity != r.Original && r.Send > 0))
        {
            ModelState.AddModelError(string.Empty, $"Change either the home count or the send amount for {both.Sku}, not both. Save one, then the other.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var user = User.Identity!.Name!; // Non-null: the page requires an authenticated user.
        var sends = Rows.Where(r => r.Send > 0).Select(r => new HomeStockShipment(r.Sku, r.Original, r.Send!.Value)).ToList(); // Non-null: filtered.
        var counts = Rows.Where(r => r.Quantity != r.Original).Select(r => new HomeStockEntry(r.Sku, r.Quantity)).ToList();

        HomeStockCountResult? shipped = null;
        if (sends.Count > 0)
        {
            var result = await ledger.ShipToAmazonAsync(Marketplace.MarketplaceId, sends, ShipmentReference, user, cancellationToken);
            if (result.IsFailure)
            {
                ModelState.AddModelError(string.Empty, result.Error);
                await LoadAsync(cancellationToken);
                return Page();
            }

            shipped = result.Value;
        }

        if (counts.Count > 0)
        {
            var result = await items.SetHomeStockAsync(Marketplace.MarketplaceId, counts, user, cancellationToken);
            if (result.IsFailure)
            {
                // Sends (if any) are already logged; say so, so nobody sends them twice.
                TempData[StatusMessage.Error] = result.Error + (shipped is { Applied: > 0 } ? $" Sends were saved ({shipped.UnitsOut:N0} units)." : string.Empty);
                return RedirectToPage(new { family = FamilyId });
            }
        }

        var done = new List<string>();
        if (shipped is { Applied: > 0 })
        {
            done.Add($"Logged {shipped.UnitsOut:N0} unit{(shipped.UnitsOut == 1 ? "" : "s")} sent to Amazon for {shipped.Applied:N0} SKU{(shipped.Applied == 1 ? "" : "s")}.");
        }

        if (counts.Count > 0)
        {
            done.Add($"Saved home stock for {counts.Count:N0} SKU{(counts.Count == 1 ? "" : "s")}.");
        }

        TempData[StatusMessage.Success] = done.Count == 0 ? "Nothing changed." : $"{string.Join(" ", done)} Each change is in the home-stock ledger.";
        if (shipped is { Stale.Count: > 0 } s)
        {
            TempData[StatusMessage.Error] =
                $"Didn't send {string.Join(", ", s.Stale.Take(10))}{(s.Stale.Count > 10 ? ", …" : "")}: home stock changed after the page loaded. Check the new count and enter the send again.";
        }

        return RedirectToPage(new { family = FamilyId });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Today = LocalTime.DateOf(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(Marketplace.TimeZoneId));
        Families = await items.ListFamiliesAsync(cancellationToken);
        Family = Families.FirstOrDefault(f => f.Id == FamilyId) ?? (Families.Count > 0 ? Families[0] : null);
        FamilyId = Family?.Id;
        if (Family is not null)
        {
            Sheet = await inventory.GetWorksheetAsync(Marketplace, Family.Id, cancellationToken);
        }
    }

    /// <summary>One SKU's count and send amount as posted.</summary>
    public sealed class RowInput
    {
        /// <summary>Seller SKU.</summary>
        public string Sku { get; set; } = string.Empty;

        /// <summary>The count shown when the page loaded, to detect changes.</summary>
        public int Original { get; set; }

        /// <summary>The count entered.</summary>
        [Range(0, InventoryItemService.MaxHomeStock, ErrorMessage = "Home stock must be between 0 and 1,000,000.")]
        public int Quantity { get; set; }

        /// <summary>Units being sent to Amazon from home stock; empty or 0 means none.</summary>
        [Range(0, InventoryItemService.MaxHomeStock, ErrorMessage = "The send amount must be between 0 and 1,000,000.")]
        public int? Send { get; set; }
    }
}
