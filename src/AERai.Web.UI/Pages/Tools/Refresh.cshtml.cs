using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Marketplaces;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Tools;

/// <summary>
/// Loads the database from files already in raw storage, without calling Amazon: upload report files or
/// Orders API pages to the raw container, then refresh. Operators and Admins only (the <c>/Tools</c> folder).
/// </summary>
/// <param name="refresh">Blob refresh use cases.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class RefreshModel(IBlobRefreshService refresh, ICurrentMarketplace currentMarketplace) : PageModel
{
    /// <summary>How many problems are shown in the banner before the rest are summarized.</summary>
    private const int ProblemsShown = 5;

    /// <summary>Marketplace used for files that do not say which one they belong to.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>What a refresh would pick up right now.</summary>
    public BlobRefreshPreview Preview { get; private set; } = default!;

    /// <summary>Whether to promote each staged report straight into the reports.</summary>
    [BindProperty]
    public bool Promote { get; set; } = true;

    /// <summary>Whether to load files that were already loaded too.</summary>
    [BindProperty]
    public bool Reprocess { get; set; }

    /// <summary>Counts the files waiting.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Preview = await refresh.PreviewAsync(new BlobRefreshRequest(Marketplace.MarketplaceId), cancellationToken);
    }

    /// <summary>Loads the waiting files.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the page with the outcome.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var result = await refresh.RefreshAsync(new BlobRefreshRequest(marketplace.MarketplaceId, Promote, Reprocess), User.Identity!.Name!, cancellationToken);
        if (result.IsFailure)
        {
            TempData[StatusMessage.Error] = result.Error;
            return RedirectToPage();
        }

        var summary = result.Value;
        TempData[StatusMessage.Success] =
            $"Loaded {summary.Loaded:N0} file{(summary.Loaded == 1 ? "" : "s")}" +
            (summary.OrdersWritten > 0 ? $", {summary.OrdersWritten:N0} orders written" : "") +
            (summary.Promoted > 0 ? $", {summary.Promoted:N0} report{(summary.Promoted == 1 ? "" : "s")} promoted" : "") +
            (summary.Empty > 0 ? $", {summary.Empty:N0} empty file{(summary.Empty == 1 ? "" : "s")} noted" : "") +
            $". {summary.AlreadyLoaded:N0} already loaded, {summary.SnapshotFiles:N0} snapshot file{(summary.SnapshotFiles == 1 ? "" : "s")} left alone.";

        var notes = new List<string>();
        if (summary.HitLimit)
        {
            notes.Add($"More than {BlobRefreshService.MaxFilesPerRefresh} files were waiting. Refresh again for the rest.");
        }

        if (summary.Problems.Count > 0)
        {
            var shown = string.Join(" | ", summary.Problems.Take(ProblemsShown));
            var more = summary.Problems.Count > ProblemsShown ? $" (+{summary.Problems.Count - ProblemsShown} more)" : "";
            notes.Add($"{summary.Problems.Count} file{(summary.Problems.Count == 1 ? "" : "s")} had a problem: {shown}{more}");
        }

        if (notes.Count > 0)
        {
            TempData[StatusMessage.Warning] = string.Join(" ", notes);
        }

        return RedirectToPage();
    }
}
