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
/// Everyone can view it; Operators and Admins can edit home stock.
/// </summary>
/// <param name="inventory">Inventory numbers and the worksheet layout.</param>
/// <param name="items">Item editing (families list, home-stock saves).</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
/// <param name="options">Restock settings (when an action counts as due soon).</param>
/// <param name="clock">Clock (the sheet is dated in the marketplace's local calendar).</param>
public sealed class WorksheetModel(IInventoryService inventory, IInventoryItemService items, ICurrentMarketplace currentMarketplace, IOptions<InventoryOptions> options, TimeProvider clock) : PageModel
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

    /// <summary>Posted home-stock counts.</summary>
    [BindProperty]
    public List<RowInput> Rows { get; set; } = [];

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

    /// <summary>Shows the worksheet.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    /// <summary>Saves changed home-stock counts; each change is logged in the ledger as a count correction.</summary>
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
        var changed = Rows.Where(r => r.Quantity != r.Original).Select(r => new HomeStockEntry(r.Sku, r.Quantity)).ToList();
        if (changed.Count > 0)
        {
            var result = await items.SetHomeStockAsync(Marketplace.MarketplaceId, changed, User.Identity!.Name!, cancellationToken);
            if (result.IsFailure)
            {
                ModelState.AddModelError(string.Empty, result.Error);
                await LoadAsync(cancellationToken);
                return Page();
            }
        }

        TempData[StatusMessage.Success] = changed.Count == 0
            ? "Nothing changed."
            : $"Saved home stock for {changed.Count:N0} SKU{(changed.Count == 1 ? "" : "s")}; each change is in the home-stock ledger.";
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

    /// <summary>One SKU's count as posted.</summary>
    public sealed class RowInput
    {
        /// <summary>Seller SKU.</summary>
        public string Sku { get; set; } = string.Empty;

        /// <summary>The count shown when the page loaded, to detect changes.</summary>
        public int Original { get; set; }

        /// <summary>The count entered.</summary>
        [Range(0, InventoryItemService.MaxHomeStock, ErrorMessage = "Home stock must be between 0 and 1,000,000.")]
        public int Quantity { get; set; }
    }
}
