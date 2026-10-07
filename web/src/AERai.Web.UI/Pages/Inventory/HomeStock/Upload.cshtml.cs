using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Security;
using AERai.Web.Domain.Core;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Inventory.HomeStock;

/// <summary>
/// The count-sheet round trip: download the sheet with current home stock, upload it filled in,
/// review every change against what's on record, then apply — which logs one ledger entry per
/// changed SKU with the movement types chosen for increases and decreases. Nothing is saved until
/// Apply. Operators and Admins only.
/// </summary>
/// <param name="templates">Builds the downloadable spreadsheet.</param>
/// <param name="items">Item service (reads and checks the uploaded sheet).</param>
/// <param name="ledger">Ledger service (applies the reviewed changes).</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
/// <param name="clock">Clock (the review form's default date is now).</param>
[Authorize(Policy = AppPolicies.RequireOperator)]
[RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
[RequestSizeLimit(MaxRequestBytes)]
public sealed class UploadModel(
    IHomeStockTemplateService templates,
    IInventoryItemService items,
    IHomeStockLedgerService ledger,
    ICurrentMarketplace currentMarketplace,
    TimeProvider clock) : PageModel
{
    /// <summary>Largest upload accepted (a 10,000-row sheet is far smaller).</summary>
    public const int MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>How many rejected rows to list; the rest are summarized.</summary>
    public const int RejectedRowsShown = 100;

    private const int MaxRequestBytes = MaxFileBytes + (256 * 1024);

    /// <summary>Marketplace the stock is imported into.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>Families for the count sheet's family picker.</summary>
    public IReadOnlyList<ProductFamily> Families { get; private set; } = [];

    /// <summary>The uploaded file.</summary>
    [BindProperty]
    public IFormFile? Spreadsheet { get; set; }

    /// <summary>The uploaded sheet checked against current home stock, shown for review.</summary>
    public HomeStockReconciliation? Preview { get; private set; }

    /// <summary>The Apply form (movement types, reference, note, date, and the reviewed changes).</summary>
    [BindProperty]
    public ApplyInput Apply { get; set; } = new();

    /// <summary>Labels for movement types offered on the review.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The label.</returns>
    public static string Label(HomeStockMovementType type) => type switch
    {
        HomeStockMovementType.ReceivedFromSupplier => "Received from supplier",
        HomeStockMovementType.ShippedToAmazon => "Shipped to Amazon",
        HomeStockMovementType.CountCorrection => "Count correction (recount)",
        _ => "Other",
    };

    /// <summary>Shows the form.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    /// <summary>
    /// Downloads the count sheet (.xlsx): every SKU, or one family's, with its current count in the
    /// <c>home-stock</c> column, ready to adjust and upload back unchanged.
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

    /// <summary>Reads the uploaded sheet and shows how it differs from current home stock. Saves nothing.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page in review state, or with errors.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        // Only the file is posted here; the Apply form's fields belong to the next step.
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith(nameof(Apply), StringComparison.Ordinal)).ToList())
        {
            ModelState.Remove(key);
        }

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
        var result = await items.PreviewHomeStockImportAsync(Marketplace.MarketplaceId, Spreadsheet.FileName, stream, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(nameof(Spreadsheet), result.Error);
            return Page();
        }

        // Rendered on this response rather than redirected: the review is too large for TempData.
        Preview = result.Value;
        Apply = new ApplyInput
        {
            Changes = HomeStockCountPayload.Write(Preview.Changes),
            OccurredAt = WholeMinute(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone()).DateTime),
        };
        return Page();
    }

    /// <summary>Applies the reviewed changes: one ledger entry per SKU, then on to the ledger.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the ledger, or the review again with the error.</returns>
    public async Task<IActionResult> OnPostApplyAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        ModelState.Remove(nameof(Spreadsheet));

        var changes = HomeStockCountPayload.Read(Apply.Changes);
        if (changes.IsFailure)
        {
            TempData[StatusMessage.Error] = changes.Error;
            return RedirectToPage();
        }

        if (ModelState.IsValid)
        {
            DateTimeOffset? at = Apply.OccurredAt is { } local ? LocalTime.ToInstant(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone()) : null;
            var options = new HomeStockCountOptions(Apply.IncreaseType, Apply.DecreaseType, Apply.Reference, Apply.Note, at);
            var result = await ledger.ApplyCountsAsync(Marketplace.MarketplaceId, changes.Value, options, User.Identity!.Name!, cancellationToken);
            if (result.IsSuccess)
            {
                var r = result.Value;
                var units = string.Join(" / ", new[] { r.UnitsIn > 0 ? $"+{r.UnitsIn:N0}" : null, r.UnitsOut > 0 ? $"−{r.UnitsOut:N0}" : null }.OfType<string>());
                TempData[StatusMessage.Success] = $"Logged {r.Applied:N0} movement{(r.Applied == 1 ? "" : "s")} from the count sheet ({units} units).";
                if (r.Stale.Count > 0)
                {
                    TempData[StatusMessage.Error] =
                        $"Skipped {r.Stale.Count:N0} SKU{(r.Stale.Count == 1 ? "" : "s")} whose home stock changed after the review: {string.Join(", ", r.Stale.Take(10))}{(r.Stale.Count > 10 ? ", …" : "")}. Download a fresh sheet to recount them.";
                }

                return RedirectToPage("Ledger");
            }

            ModelState.AddModelError(string.Empty, result.Error);
        }

        // Show the same review again (rebuilt from the posted changes) so the user can fix the form.
        Preview = new HomeStockReconciliation(changes.Value, 0, []);
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Families = await items.ListFamiliesAsync(cancellationToken);
    }

    private TimeZoneInfo Zone() => TimeZoneInfo.FindSystemTimeZoneById(Marketplace.TimeZoneId);

    // Whole minutes: the date-time picker shows seconds and milliseconds otherwise.
    private static DateTime WholeMinute(DateTime value) => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Unspecified);

    /// <summary>The Apply form.</summary>
    public sealed class ApplyInput
    {
        /// <summary>The reviewed changes (see <see cref="HomeStockCountPayload"/>).</summary>
        public string? Changes { get; set; }

        /// <summary>How increases are logged.</summary>
        public HomeStockMovementType IncreaseType { get; set; } = HomeStockMovementType.ReceivedFromSupplier;

        /// <summary>How decreases are logged.</summary>
        public HomeStockMovementType DecreaseType { get; set; } = HomeStockMovementType.ShippedToAmazon;

        /// <summary>Reference applied to every entry.</summary>
        [StringLength(HomeStockLedgerService.MaxReferenceLength)]
        public string? Reference { get; set; }

        /// <summary>Note applied to every entry.</summary>
        [StringLength(HomeStockLedgerService.MaxNoteLength)]
        public string? Note { get; set; }

        /// <summary>When the units moved, in the marketplace's local time.</summary>
        public DateTime? OccurredAt { get; set; }
    }
}
