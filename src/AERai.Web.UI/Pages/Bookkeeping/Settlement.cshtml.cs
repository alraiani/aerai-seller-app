using AERai.Web.Application.Bookkeeping;
using AERai.Web.Application.Marketplaces;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Bookkeeping;

/// <summary>
/// Shows how one settlement would post to QuickBooks and exports it as an IIF file.
/// </summary>
/// <param name="bookkeeping">Bookkeeping use cases.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class SettlementModel(IBookkeepingService bookkeeping, ICurrentMarketplace currentMarketplace) : PageModel
{
    /// <summary>The settlement as it would be exported.</summary>
    public SettlementPreview Preview { get; private set; } = default!;

    /// <summary>Files already generated for this settlement, newest first (exporting again adds another).</summary>
    public IReadOnlyList<BookkeepingExportSummary> PreviousExports { get; private set; } = [];

    /// <summary>The marketplace's time zone, for showing when a file was generated.</summary>
    public TimeZoneInfo Zone { get; private set; } = TimeZoneInfo.Utc;

    /// <summary>Loads the preview.</summary>
    /// <param name="id">Settlement id from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or 404 when the settlement is not in the current marketplace.</returns>
    public async Task<IActionResult> OnGetAsync(string id, CancellationToken cancellationToken)
    {
        return await LoadAsync(id, cancellationToken) ? Page() : NotFound();
    }

    /// <summary>Generates the IIF file.</summary>
    /// <param name="id">Settlement id from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the file list on success; the page again with the reason otherwise.</returns>
    public async Task<IActionResult> OnPostExportAsync(string id, CancellationToken cancellationToken)
    {
        var marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var result = await bookkeeping.ExportAsync(marketplace.MarketplaceId, id, User.Identity?.Name ?? "unknown", cancellationToken);
        if (result.IsFailure)
        {
            TempData[StatusMessage.Error] = result.Error;
            return RedirectToPage(new { id });
        }

        TempData[StatusMessage.Success] = $"Exported settlement {id} as {result.Value.FileName}.";
        return RedirectToPage("Index");
    }

    private async Task<bool> LoadAsync(string id, CancellationToken cancellationToken)
    {
        var marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var result = await bookkeeping.PreviewAsync(marketplace.MarketplaceId, id, cancellationToken);
        if (result.IsFailure)
        {
            return false;
        }

        Preview = result.Value;
        Zone = TimeZoneInfo.FindSystemTimeZoneById(marketplace.TimeZoneId);
        // Re-exporting is allowed (the file may have been lost), but the same entry imported twice would
        // double the books, so the page shows what was handed over before and asks first.
        PreviousExports = (await bookkeeping.GetExportsAsync(marketplace.MarketplaceId, cancellationToken))
            .Where(e => e.SettlementId == id)
            .OrderByDescending(e => e.GeneratedAt)
            .ToList();
        return true;
    }
}
