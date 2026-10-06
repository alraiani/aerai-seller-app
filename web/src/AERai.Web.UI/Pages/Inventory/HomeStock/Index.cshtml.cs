using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Security;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Inventory.HomeStock;

/// <summary>
/// Types in home stock for many SKUs at once: one number per SKU, one Save for the page. Only
/// changed rows are saved. Operators and Admins only.
/// </summary>
/// <param name="inventory">Inventory service (lists the SKUs).</param>
/// <param name="items">Item editing service (saves the counts).</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
[Authorize(Policy = AppPolicies.RequireOperator)]
public sealed class IndexModel(IInventoryService inventory, IInventoryItemService items, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>Rows per page; larger than other lists so a count can be entered in one pass.</summary>
    public const int PageSize = 100;

    /// <summary>Marketplace whose home stock is edited.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>The SKUs on this page.</summary>
    public PagedResult<InventoryItem> Skus { get; private set; } = default!;

    /// <summary>Posted counts.</summary>
    [BindProperty]
    public List<RowInput> Rows { get; set; } = [];

    /// <summary>The value to show in a SKU's box: what the user typed when a save failed, otherwise the saved count.</summary>
    /// <param name="item">The SKU's row.</param>
    /// <returns>The value for the input.</returns>
    public string ValueFor(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        // Re-show posted input after a failed save so the user's other edits are not lost.
        var key = $"{nameof(Rows)}[{Rows.FindIndex(r => r.Sku == item.Sku)}].{nameof(RowInput.Quantity)}";
        return ModelState.TryGetValue(key, out var entry) && entry.AttemptedValue is { } typed
            ? typed
            : item.Position.HomeStock.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Lists the SKUs with their current home stock.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    /// <summary>Saves the rows whose count changed.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the same page on success; the page with errors otherwise.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
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
            : $"Saved home stock for {changed.Count:N0} SKU{(changed.Count == 1 ? "" : "s")} in {Marketplace.Name}.";
        return RedirectToPage(new { p = PageNumber, q = Search });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var overview = await inventory.GetOverviewAsync(Marketplace, new PageRequest(PageNumber, PageSize, Search), null, InventorySort.Sku, cancellationToken);
        Skus = overview.Items;
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
