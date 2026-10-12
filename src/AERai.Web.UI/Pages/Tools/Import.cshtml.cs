using System.ComponentModel.DataAnnotations;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Domain.Staging;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AERai.Web.UI.Pages.Tools;

/// <summary>
/// Upload tool: loads one or more report files into the raw staging schema, one new batch per file.
/// </summary>
/// <param name="importService">Staging import use case.</param>
/// <param name="promotion">Promotes a staged batch when the user asked for that.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing (the default for uploads).</param>
/// <param name="options">Upload limits (shown on the page).</param>
// The service enforces ImportOptions.MaxFileBytes per file precisely; these outer caps stop oversized
// bodies before they are buffered at all (one file's limit plus multipart headroom, and a ceiling for the lot).
[RequestSizeLimit(ImportPageLimits.MaxRequestBytes)]
[RequestFormLimits(MultipartBodyLengthLimit = ImportPageLimits.MaxFileRequestBytes)]
public sealed class ImportModel(IStagingImportService importService, IPromotionService promotion, ICurrentMarketplace currentMarketplace, IOptions<ImportOptions> options) : PageModel
{
    /// <summary>Most files one upload accepts; upload the rest in another go.</summary>
    public const int MaxFilesPerUpload = 50;

    /// <summary>How many failures are listed in the banner before the rest are summarized.</summary>
    private const int FailuresShown = 5;

    /// <summary>Posted form values.</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Upload limits for display.</summary>
    public ImportOptions Limits => options.Value;

    /// <summary>Marketplaces the files can be imported into (active ones only).</summary>
    public IReadOnlyList<Domain.Core.Marketplace> Marketplaces { get; private set; } = [];

    /// <summary>Shows the form, defaulting the marketplace to the one being viewed.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var selection = await LoadMarketplacesAsync(cancellationToken);
        Input.MarketplaceId = selection.Current.MarketplaceId;
    }

    /// <summary>Stages each uploaded file (and promotes it when asked) and redirects to the result.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect on success; the page with errors when nothing could be staged.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadMarketplacesAsync(cancellationToken);
        if (!Marketplaces.Any(m => m.MarketplaceId == Input.MarketplaceId))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.MarketplaceId)}", "Choose an active marketplace.");
        }

        var files = Input.Files.Where(f => f.Length > 0 || !string.IsNullOrEmpty(f.FileName)).ToList();
        if (files.Count == 0)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Files)}", "Choose at least one file to import.");
        }
        else if (files.Count > MaxFilesPerUpload)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Files)}", $"Choose at most {MaxFilesPerUpload} files at a time ({files.Count} were chosen).");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var staged = new List<ImportReceipt>();
        var failures = new List<string>();
        var promotedCount = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // With no report type chosen, each file says what it is through its columns.
            var source = Input.Source;
            if (source is null)
            {
                await using var probe = file.OpenReadStream();
                var detected = await importService.DetectSourceAsync(probe, cancellationToken);
                if (detected.IsFailure)
                {
                    failures.Add($"{file.FileName}: {detected.Error}");
                    continue;
                }

                source = detected.Value;
            }

            await using var stream = file.OpenReadStream();
            var result = await importService.ImportAsync(
                new ImportFileCommand(source.Value, Input.MarketplaceId, file.FileName, file.Length, stream, User.Identity!.Name!), cancellationToken);
            if (result.IsFailure)
            {
                failures.Add($"{file.FileName}: {result.Error}");
                continue;
            }

            staged.Add(result.Value);
            if (!Input.PromoteAfterStaging)
            {
                continue;
            }

            var promoted = await promotion.PromoteAsync(result.Value.BatchId, cancellationToken);
            if (promoted.IsSuccess)
            {
                promotedCount++;
            }
            else
            {
                failures.Add($"{file.FileName}: staged as batch {result.Value.BatchId}, but promotion failed: {promoted.Error}");
            }
        }

        if (staged.Count == 0)
        {
            foreach (var failure in failures)
            {
                ModelState.AddModelError(string.Empty, failure);
            }

            return Page();
        }

        if (failures.Count > 0)
        {
            var shown = string.Join(" | ", failures.Take(FailuresShown));
            var more = failures.Count > FailuresShown ? $" (+{failures.Count - FailuresShown} more)" : string.Empty;
            TempData[StatusMessage.Warning] = $"{failures.Count} problem{(failures.Count == 1 ? "" : "s")}: {shown}{more}";
        }

        // One file keeps the old flow: straight to its batch, to review and promote. Several go to the batch list.
        if (staged.Count == 1)
        {
            var only = staged[0];
            TempData[StatusMessage.Success] = promotedCount == 1
                ? $"Staged {only.RowCount:N0} rows as batch {only.BatchId} and promoted them."
                : $"Staged {only.RowCount:N0} rows as batch {only.BatchId}. Review and promote it below.";
            return RedirectToPage("/Tools/Batches/Details", new { id = only.BatchId });
        }

        var rows = staged.Sum(s => s.RowCount);
        TempData[StatusMessage.Success] =
            $"Staged {staged.Count:N0} files ({rows:N0} rows)" +
            (Input.PromoteAfterStaging ? $", {promotedCount:N0} promoted" : ". Open each batch to review and promote it") + ".";
        return RedirectToPage("/Tools/Batches/Index");
    }

    private async Task<MarketplaceSelection> LoadMarketplacesAsync(CancellationToken cancellationToken)
    {
        var selection = await currentMarketplace.GetAsync(cancellationToken);
        Marketplaces = [.. selection.All.Where(m => m.IsActive)];
        return selection;
    }

    /// <summary>Upload form fields.</summary>
    public sealed class InputModel
    {
        /// <summary>Report type every file contains; <see langword="null"/> detects it from each file's columns.</summary>
        [Display(Name = "Report type")]
        public ImportSource? Source { get; set; }

        /// <summary>Marketplace the files' data belongs to.</summary>
        [Required]
        [Display(Name = "Marketplace")]
        public string MarketplaceId { get; set; } = string.Empty;

        /// <summary>The uploaded files, one batch each.</summary>
        [Display(Name = "Files")]
        public List<IFormFile> Files { get; set; } = [];

        /// <summary>Whether to promote each file into the reports as soon as it is staged.</summary>
        [Display(Name = "Promote each file right after staging")]
        public bool PromoteAfterStaging { get; set; }
    }
}

/// <summary>Request-size constants for the import page (attribute arguments must be constants).</summary>
internal static class ImportPageLimits
{
    /// <summary>25 MB: the 20 MB default per-file limit plus multipart headroom.</summary>
    public const long MaxFileRequestBytes = 25L * 1024 * 1024;

    /// <summary>250 MB for the whole upload (the per-file limit is enforced by the service).</summary>
    public const long MaxRequestBytes = 250L * 1024 * 1024;
}
