using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Security;
using AERai.Web.Domain.Core;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AERai.Web.UI.Pages.Inventory;

/// <summary>
/// Edits the user-maintained parts of one SKU: its family and picture (shared by all marketplaces)
/// and its home stock and lead times in the current marketplace. Operators and Admins only.
/// </summary>
/// <param name="items">Item editing service.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
/// <param name="options">Restock-planning defaults (shown as placeholders).</param>
[Authorize(Policy = AppPolicies.RequireOperator)]
// The service enforces the 2 MB picture limit precisely; this outer cap stops larger bodies early.
[RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
[RequestSizeLimit(MaxRequestBytes)]
public sealed class EditModel(IInventoryItemService items, ICurrentMarketplace currentMarketplace, IOptions<InventoryOptions> options) : PageModel
{
    private const int MaxRequestBytes = InventoryItemService.MaxImageBytes + (256 * 1024);

    /// <summary>Marketplace whose home stock is edited.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>The SKU being edited.</summary>
    public InventoryItemDetails Item { get; private set; } = default!;

    /// <summary>The app-wide lead-time defaults, shown as placeholders.</summary>
    public InventoryOptions Defaults => options.Value;

    /// <summary>Existing families, offered as suggestions.</summary>
    public IReadOnlyList<ProductFamily> Families { get; private set; } = [];

    /// <summary>Posted details.</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Posted picture.</summary>
    [BindProperty]
    public IFormFile? Picture { get; set; }

    /// <summary>Loads the SKU.</summary>
    /// <param name="sku">Seller SKU from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or 404 when the SKU is unknown.</returns>
    public async Task<IActionResult> OnGetAsync(string sku, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(sku, cancellationToken))
        {
            return NotFound();
        }

        Input.Family = Item.Family;
        Input.Color = Item.Color;
        Input.HomeStock = Item.HomeStock;
        (Input.SupplierLeadTimeDays, Input.PrepTimeDays, Input.TransitDays, Input.SafetyStockDays, Input.TargetStockDays) =
            (Item.LeadTimes.SupplierLeadTimeDays, Item.LeadTimes.PrepTimeDays, Item.LeadTimes.TransitDays, Item.LeadTimes.SafetyStockDays, Item.LeadTimes.TargetStockDays);
        return Page();
    }

    /// <summary>Saves the family and home stock.</summary>
    /// <param name="sku">Seller SKU from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the inventory list on success; the page with errors otherwise.</returns>
    public async Task<IActionResult> OnPostAsync(string sku, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(sku, cancellationToken))
        {
            return NotFound();
        }

        // Only the details form is validated here; the picture has its own handler.
        ModelState.Remove(nameof(Picture));
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var update = new InventoryItemUpdate(
            Input.Family,
            Input.HomeStock,
            new LeadTimeSettings(Input.SupplierLeadTimeDays, Input.PrepTimeDays, Input.TransitDays, Input.SafetyStockDays, Input.TargetStockDays),
            Input.Color);
        var result = await items.UpdateAsync(sku, Marketplace.MarketplaceId, update, User.Identity!.Name!, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return Page();
        }

        TempData[StatusMessage.Success] = $"{sku} saved.";
        return RedirectToPage("Index");
    }

    /// <summary>Uploads a new picture.</summary>
    /// <param name="sku">Seller SKU from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to this page.</returns>
    public async Task<IActionResult> OnPostPictureAsync(string sku, CancellationToken cancellationToken)
    {
        if (Picture is null)
        {
            TempData[StatusMessage.Error] = "Choose a picture to upload.";
            return RedirectToPage(new { sku });
        }

        await using var stream = Picture.OpenReadStream();
        var result = await items.SetImageAsync(sku, stream, Picture.Length, cancellationToken);
        TempData[result.IsSuccess ? StatusMessage.Success : StatusMessage.Error] = result.IsSuccess ? "Picture updated." : result.Error;
        return RedirectToPage(new { sku });
    }

    /// <summary>Removes the picture.</summary>
    /// <param name="sku">Seller SKU from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to this page.</returns>
    public async Task<IActionResult> OnPostRemovePictureAsync(string sku, CancellationToken cancellationToken)
    {
        var result = await items.RemoveImageAsync(sku, cancellationToken);
        TempData[result.IsSuccess ? StatusMessage.Success : StatusMessage.Error] = result.IsSuccess ? "Picture removed." : result.Error;
        return RedirectToPage(new { sku });
    }

    private async Task<bool> LoadAsync(string sku, CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        if (await items.GetAsync(sku, Marketplace.MarketplaceId, cancellationToken) is not { } item)
        {
            return false;
        }

        Item = item;
        Families = await items.ListFamiliesAsync(cancellationToken);
        return true;
    }

    /// <summary>Editable fields.</summary>
    public sealed class InputModel
    {
        /// <summary>Family name; a new name creates the family, blank clears it.</summary>
        [Display(Name = "Family")]
        [StringLength(FamilyNames.MaxLength)]
        public string? Family { get; set; }

        /// <summary>The variant's color, if any.</summary>
        [Display(Name = "Color")]
        public ProductColor? Color { get; set; }

        /// <summary>Units held outside Amazon for the marketplace.</summary>
        [Display(Name = "Home stock (units)")]
        [Range(0, InventoryItemService.MaxHomeStock)]
        public int HomeStock { get; set; }

        /// <summary>Days from ordering to arrival; blank uses the default.</summary>
        [Display(Name = "Supplier lead time")]
        [Range(0, InventoryItemService.MaxLeadTimeDays)]
        public int? SupplierLeadTimeDays { get; set; }

        /// <summary>Days to prepare for shipping; blank uses the default.</summary>
        [Display(Name = "Prep time")]
        [Range(0, InventoryItemService.MaxLeadTimeDays)]
        public int? PrepTimeDays { get; set; }

        /// <summary>Days from shipping until sellable at Amazon; blank uses the default.</summary>
        [Display(Name = "Transit to Amazon")]
        [Range(0, InventoryItemService.MaxLeadTimeDays)]
        public int? TransitDays { get; set; }

        /// <summary>Buffer days before the projected stockout; blank uses the default.</summary>
        [Display(Name = "Safety stock")]
        [Range(0, InventoryItemService.MaxLeadTimeDays)]
        public int? SafetyStockDays { get; set; }

        /// <summary>Days of sales a restock should cover; blank uses the default.</summary>
        [Display(Name = "Target cover")]
        [Range(1, InventoryItemService.MaxLeadTimeDays)]
        public int? TargetStockDays { get; set; }
    }
}
