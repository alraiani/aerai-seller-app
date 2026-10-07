using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Security;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Inventory.HomeStock;

/// <summary>
/// Uploads home stock from a spreadsheet (.xlsx, .csv, or .tsv with columns sku and home-stock) and
/// offers a template pre-filled with the marketplace's SKUs. Operators and Admins only.
/// </summary>
/// <param name="templates">Builds the downloadable spreadsheet.</param>
/// <param name="items">Item editing service (imports the file).</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
[Authorize(Policy = AppPolicies.RequireOperator)]
[RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
[RequestSizeLimit(MaxRequestBytes)]
public sealed class UploadModel(IHomeStockTemplateService templates, IInventoryItemService items, ICurrentMarketplace currentMarketplace) : PageModel
{
    /// <summary>Largest upload accepted (a 10,000-row sheet is far smaller).</summary>
    public const int MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>How many rejected rows to list; the rest are summarized.</summary>
    public const int RejectedRowsShown = 100;

    private const int MaxRequestBytes = MaxFileBytes + (256 * 1024);

    /// <summary>Marketplace the stock is imported into.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>The uploaded file.</summary>
    [BindProperty]
    public IFormFile? Spreadsheet { get; set; }

    /// <summary>The outcome of the upload just made, shown below the form.</summary>
    public HomeStockImportResult? Outcome { get; private set; }

    /// <summary>Shows the form.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Families = await items.ListFamiliesAsync(cancellationToken);
    }

    /// <summary>Families for the template's family picker.</summary>
    public IReadOnlyList<Domain.Core.ProductFamily> Families { get; private set; } = [];

    /// <summary>
    /// Downloads the home-stock spreadsheet (.xlsx): every SKU, or one family's, with its current
    /// count in the <c>home-stock</c> column, ready to fill in and upload back unchanged.
    /// </summary>
    /// <param name="family">Only this family's SKUs, or <see langword="null"/> for all.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The workbook.</returns>
    public async Task<IActionResult> OnGetTemplateAsync(int? family, CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var (fileName, content) = await templates.CreateAsync(Marketplace, family, cancellationToken);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>Imports the uploaded file.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page with the outcome, or with errors.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Families = await items.ListFamiliesAsync(cancellationToken);
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
        var result = await items.ImportHomeStockAsync(Marketplace.MarketplaceId, Spreadsheet.FileName, stream, User.Identity!.Name!, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(nameof(Spreadsheet), result.Error);
            return Page();
        }

        // Shown on this response rather than redirected: the rejected-row list is too large for TempData.
        Outcome = result.Value;
        return Page();
    }
}
