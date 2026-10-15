using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Products;
using AERai.Web.Application.Security;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Products;

/// <summary>
/// The COGS sheet round trip: download the sheet with current costs, upload it filled in, review
/// every change against the costs on record, then apply. Nothing is saved until Apply. Operators
/// and Admins only.
/// </summary>
/// <param name="templates">Builds the downloadable sheet.</param>
/// <param name="productService">Checks the uploaded sheet and saves the reviewed costs.</param>
/// <param name="families">Families for the sheet's family picker.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
[Authorize(Policy = AppPolicies.RequireOperator)]
[RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
[RequestSizeLimit(MaxRequestBytes)]
public sealed class UploadModel(
    IProductCostTemplateService templates,
    IProductService productService,
    IProductFamilyService families,
    ICurrentMarketplace currentMarketplace) : PageModel
{
    /// <summary>Largest upload accepted (a 10,000-row sheet is far smaller).</summary>
    public const int MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>How many rejected rows to list; the rest are summarized.</summary>
    public const int RejectedRowsShown = 100;

    private const int MaxRequestBytes = MaxFileBytes + (256 * 1024);

    /// <summary>Marketplace the costs apply to (they are in its currency).</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>Families for the sheet's family picker.</summary>
    public IReadOnlyList<FamilySummary> Families { get; private set; } = [];

    /// <summary>The uploaded file.</summary>
    [BindProperty]
    public IFormFile? Spreadsheet { get; set; }

    /// <summary>The uploaded sheet checked against current costs, shown for review.</summary>
    public ProductCostReconciliation? Preview { get; private set; }

    /// <summary>The reviewed changes (see <see cref="ProductCostPayload"/>), carried to Apply.</summary>
    [BindProperty]
    public string? Changes { get; set; }

    /// <summary>Shows the form.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    /// <summary>
    /// Downloads the COGS sheet (.xlsx): every product, or one family's, with its current cost in the
    /// <c>cost-of-goods</c> column, ready to edit and upload back unchanged.
    /// </summary>
    /// <param name="family">Only this family's products, or <see langword="null"/> for all.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The workbook.</returns>
    public async Task<IActionResult> OnGetTemplateAsync(int? family, CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var (fileName, content) = await templates.CreateAsync(Marketplace, family, cancellationToken);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>Reads the uploaded sheet and shows how it differs from current costs. Saves nothing.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page in review state, or with errors.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        ModelState.Remove(nameof(Changes));

        if (Spreadsheet is null || Spreadsheet.Length == 0)
        {
            ModelState.AddModelError(nameof(Spreadsheet), "Choose a file to upload.");
            return Page();
        }

        if (Spreadsheet.Length > MaxFileBytes)
        {
            ModelState.AddModelError(nameof(Spreadsheet), $"Files can be at most {MaxFileBytes / (1024 * 1024)} MB.");
            return Page();
        }

        await using var stream = Spreadsheet.OpenReadStream();
        var result = await productService.PreviewCostImportAsync(Marketplace.MarketplaceId, Spreadsheet.FileName, stream, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(nameof(Spreadsheet), result.Error);
            return Page();
        }

        // Rendered on this response rather than redirected: the review is too large for TempData.
        Preview = result.Value;
        Changes = ProductCostPayload.Write(Preview.Changes);
        return Page();
    }

    /// <summary>Saves the reviewed changes, then returns to the product list.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the product list, or back to this page with the error.</returns>
    public async Task<IActionResult> OnPostApplyAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;

        var changes = ProductCostPayload.Read(Changes);
        if (changes.IsFailure)
        {
            TempData[StatusMessage.Error] = changes.Error;
            return RedirectToPage();
        }

        var result = await productService.SaveCostsAsync(Marketplace.MarketplaceId, changes.Value, cancellationToken);
        if (result.IsFailure)
        {
            TempData[StatusMessage.Error] = result.Error;
            return RedirectToPage();
        }

        var r = result.Value;
        TempData[StatusMessage.Success] = $"Saved {r.Applied:N0} {Marketplace.Code} cost{(r.Applied == 1 ? "" : "s")} of goods from the sheet.";
        if (r.Stale.Count > 0)
        {
            TempData[StatusMessage.Error] =
                $"Skipped {r.Stale.Count:N0} SKU{(r.Stale.Count == 1 ? "" : "s")} whose cost changed after the review: {string.Join(", ", r.Stale.Take(10))}{(r.Stale.Count > 10 ? ", …" : "")}. Download a fresh sheet to update them.";
        }

        return RedirectToPage("Index", new { sort = nameof(ProductSort.Updated) });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Families = await families.ListAsync(cancellationToken);
    }
}
