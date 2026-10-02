using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Products;
using AERai.Web.Application.Security;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Products;

/// <summary>
/// Edits a product's cost of goods. Operators and Admins only.
/// </summary>
/// <param name="products">Product queries.</param>
/// <param name="productService">Product use cases.</param>
[Authorize(Policy = AppPolicies.RequireOperator)]
public sealed class EditModel(IProductRepository products, IProductService productService) : PageModel
{
    /// <summary>The product being edited (for display).</summary>
    public ProductSummary Product { get; private set; } = default!;

    /// <summary>Posted form values.</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Loads the product.</summary>
    /// <param name="sku">Seller SKU from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or 404 when the SKU is unknown.</returns>
    public async Task<IActionResult> OnGetAsync(string sku, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(sku, cancellationToken))
        {
            return NotFound();
        }

        Input.CostOfGoods = Product.CostOfGoods;
        return Page();
    }

    /// <summary>Saves the new cost.</summary>
    /// <param name="sku">Seller SKU from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the product list on success; the page with errors otherwise.</returns>
    public async Task<IActionResult> OnPostAsync(string sku, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(sku, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await productService.UpdateCostAsync(sku, Input.CostOfGoods, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return Page();
        }

        TempData[StatusMessage.Success] = $"Cost of goods for {sku} saved.";
        return RedirectToPage("Index");
    }

    private async Task<bool> LoadAsync(string sku, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(sku, cancellationToken);
        if (product is null)
        {
            return false;
        }

        Product = product;
        return true;
    }

    /// <summary>Editable fields.</summary>
    public sealed class InputModel
    {
        /// <summary>Unit cost; blank clears it.</summary>
        [Display(Name = "Cost of goods (per unit)")]
        [Range(0, 100_000)]
        public decimal? CostOfGoods { get; set; }
    }
}
