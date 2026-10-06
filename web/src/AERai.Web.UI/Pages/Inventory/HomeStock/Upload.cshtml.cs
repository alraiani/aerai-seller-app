using System.Globalization;
using System.Text;
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
/// <param name="inventory">Inventory service (for the template).</param>
/// <param name="items">Item editing service (imports the file).</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
[Authorize(Policy = AppPolicies.RequireOperator)]
[RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
[RequestSizeLimit(MaxRequestBytes)]
public sealed class UploadModel(IInventoryService inventory, IInventoryItemService items, ICurrentMarketplace currentMarketplace) : PageModel
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
    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;

    /// <summary>Downloads a CSV template listing the marketplace's SKUs and current home stock.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The CSV file.</returns>
    public async Task<IActionResult> OnGetTemplateAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var all = await inventory.GetItemsAsync(Marketplace, cancellationToken);

        var csv = new StringBuilder("sku,home-stock,product-name\n");
        foreach (var item in all.OrderBy(i => i.Sku, StringComparer.Ordinal))
        {
            csv.Append(CultureInfo.InvariantCulture, $"{Csv(item.Sku)},{item.Position.HomeStock},{Csv(item.Position.Title ?? string.Empty)}\n");
        }

        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", $"home-stock-{Marketplace.Code}.csv");
    }

    /// <summary>Imports the uploaded file.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page with the outcome, or with errors.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
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

    /// <summary>Quotes a CSV field when it contains a delimiter, quote, or line break (RFC 4180).</summary>
    private static string Csv(string value) =>
        value.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
}
