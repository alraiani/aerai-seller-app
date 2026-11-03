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
/// stock, what to send in and by when, home stock, and the next supplier order. Everyone can view it;
/// Operators and Admins enter home-stock changes (units added or removed) and log units sent to Amazon.
/// </summary>
/// <param name="inventory">Inventory numbers and the worksheet layout.</param>
/// <param name="items">Item editing (families list).</param>
/// <param name="ledger">Home-stock ledger (logs changes and units sent to Amazon).</param>
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

    /// <summary>A send still awaiting Amazon after this many days is flagged.</summary>
    public int PendingExpiryDays => options.Value.PendingSendExpiryDays;

    /// <summary>Restock actions due within this many days are highlighted.</summary>
    public int SoonDays => options.Value.AlertLeadDays;

    /// <summary>An amount in the marketplace's currency.</summary>
    /// <param name="amount">The amount.</param>
    /// <returns>e.g. "$6.00".</returns>
    public string Money(decimal amount) => DashboardFormat.Price(amount, Marketplace.Currency);

    /// <summary>Whether the user can edit home stock.</summary>
    public bool CanEdit => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Operator);

    /// <summary>Posted home-stock changes and send amounts.</summary>
    [BindProperty]
    public List<RowInput> Rows { get; set; } = [];

    /// <summary>FBA shipment id recorded on every send, if given.</summary>
    [BindProperty]
    [StringLength(HomeStockLedgerService.MaxReferenceLength, ErrorMessage = "The shipment id can be at most 100 characters.")]
    public string? ShipmentReference { get; set; }

    /// <summary>How units added are logged (Received from supplier, Count correction, or Other).</summary>
    [BindProperty]
    public HomeStockMovementType IncreaseType { get; set; } = HomeStockMovementType.ReceivedFromSupplier;

    /// <summary>How units removed are logged (Count correction or Other; sends have their own box).</summary>
    [BindProperty]
    public HomeStockMovementType DecreaseType { get; set; } = HomeStockMovementType.CountCorrection;

    /// <summary>Note recorded on every change (required when either type is Other).</summary>
    [BindProperty]
    [StringLength(HomeStockLedgerService.MaxNoteLength, ErrorMessage = "The note can be at most 500 characters.")]
    public string? ChangeNote { get; set; }


    /// <summary>The value for a SKU's change box: what was typed when a save failed, otherwise empty.</summary>
    /// <param name="item">The SKU's row.</param>
    /// <returns>The input value.</returns>
    public string ChangeValueFor(InventoryItem item) => TypedValue(item, nameof(RowInput.Change));

    /// <summary>The value for a SKU's send box: what was typed when a save failed, otherwise empty.</summary>
    /// <param name="item">The SKU's row.</param>
    /// <returns>The input value.</returns>
    public string SendValueFor(InventoryItem item) => TypedValue(item, nameof(RowInput.Send));

    /// <summary>Shows the worksheet.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    /// <summary>
    /// Saves the sheet: home-stock changes are logged with the chosen types, then send amounts as Shipped
    /// to Amazon (home stock goes down by the amount sent). A row may have both; its send comes out of
    /// the count after its change.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the worksheet, or the page with errors.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var user = User.Identity!.Name!; // Non-null: the page requires an authenticated user.
        var rows = Rows.Select(r => new HomeStockWorksheetRow(r.Sku, r.Original, r.Change, r.Send)).ToList();
        var options = new HomeStockWorksheetOptions(IncreaseType, DecreaseType, ChangeNote, ShipmentReference);
        var result = await ledger.SaveWorksheetAsync(Marketplace.MarketplaceId, rows, options, user, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            await LoadAsync(cancellationToken);
            return Page();
        }

        var (changed, shipped, sendError) = result.Value;
        var done = new List<string>();
        if (changed is { Applied: > 0 })
        {
            var units = new List<string>();
            if (changed.UnitsIn > 0)
            {
                units.Add($"added {changed.UnitsIn:N0}");
            }

            if (changed.UnitsOut > 0)
            {
                units.Add($"removed {changed.UnitsOut:N0}");
            }

            var summary = string.Join(" and ", units);
            done.Add($"{char.ToUpperInvariant(summary[0])}{summary[1..]} home-stock unit{(changed.UnitsIn + changed.UnitsOut == 1 ? "" : "s")} for {changed.Applied:N0} SKU{(changed.Applied == 1 ? "" : "s")}.");
        }

        if (shipped is { Applied: > 0 })
        {
            done.Add($"Logged {shipped.UnitsOut:N0} unit{(shipped.UnitsOut == 1 ? "" : "s")} sent to Amazon for {shipped.Applied:N0} SKU{(shipped.Applied == 1 ? "" : "s")}; they count as awaiting Amazon until its numbers show them.");
        }

        TempData[StatusMessage.Success] = done.Count == 0 ? "Nothing changed." : $"{string.Join(" ", done)} Each change is in the home-stock ledger.";
        var problems = new List<string>();
        if (sendError is not null)
        {
            // The changes are already logged; say so, so nobody enters them twice.
            problems.Add($"Sends weren't saved: {sendError} The home-stock changes were.");
        }

        if (result.Value.Stale is { Count: > 0 } stale)
        {
            problems.Add($"Didn't save {string.Join(", ", stale.Take(10))}{(stale.Count > 10 ? ", …" : "")}: home stock changed after the page loaded. Check the new count and enter the change or send again.");
        }

        if (problems.Count > 0)
        {
            TempData[StatusMessage.Error] = string.Join(" ", problems);
        }

        return RedirectToPage(new { family = FamilyId });
    }

    private string TypedValue(InventoryItem item, string field)
    {
        ArgumentNullException.ThrowIfNull(item);

        var index = Rows.FindIndex(r => r.Sku == item.Sku);
        var key = $"{nameof(Rows)}[{index}].{field}";
        return index >= 0 && ModelState.TryGetValue(key, out var entry) && entry.AttemptedValue is { } typed ? typed : string.Empty;
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

    /// <summary>One SKU's change and send amount as posted.</summary>
    public sealed class RowInput
    {
        /// <summary>Seller SKU.</summary>
        public string Sku { get; set; } = string.Empty;

        /// <summary>The count shown when the page loaded; changes and sends are checked against it.</summary>
        public int Original { get; set; }

        /// <summary>Units added (positive) or removed (negative); empty or 0 means no change.</summary>
        [Range(-InventoryItemService.MaxHomeStock, InventoryItemService.MaxHomeStock, ErrorMessage = "A change must be between -1,000,000 and 1,000,000.")]
        public int? Change { get; set; }

        /// <summary>Units being sent to Amazon from home stock; empty or 0 means none.</summary>
        [Range(0, InventoryItemService.MaxHomeStock, ErrorMessage = "The send amount must be between 0 and 1,000,000.")]
        public int? Send { get; set; }
    }
}
