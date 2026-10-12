using System.ComponentModel.DataAnnotations;
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
/// Upload tool: loads a report file into the raw staging schema as a new batch.
/// </summary>
/// <param name="importService">Staging import use case.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing (the default for uploads).</param>
/// <param name="options">Upload limits (shown on the page).</param>
// The service enforces ImportOptions.MaxFileBytes precisely; this outer cap stops oversized
// bodies before they are buffered at all (limit + headroom for multipart overhead).
[RequestSizeLimit(ImportPageLimits.MaxRequestBytes)]
[RequestFormLimits(MultipartBodyLengthLimit = ImportPageLimits.MaxRequestBytes)]
public sealed class ImportModel(IStagingImportService importService, ICurrentMarketplace currentMarketplace, IOptions<ImportOptions> options) : PageModel
{
    /// <summary>Posted form values.</summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Upload limits for display.</summary>
    public ImportOptions Limits => options.Value;

    /// <summary>Marketplaces the file can be imported into (active ones only).</summary>
    public IReadOnlyList<Domain.Core.Marketplace> Marketplaces { get; private set; } = [];

    /// <summary>Shows the form, defaulting the marketplace to the one being viewed.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var selection = await LoadMarketplacesAsync(cancellationToken);
        Input.MarketplaceId = selection.Current.MarketplaceId;
    }

    /// <summary>Stages the uploaded file and redirects to the new batch.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the batch on success; the page with errors otherwise.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadMarketplacesAsync(cancellationToken);
        if (!Marketplaces.Any(m => m.MarketplaceId == Input.MarketplaceId))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.MarketplaceId)}", "Choose an active marketplace.");
        }

        if (!ModelState.IsValid || Input.File is null)
        {
            return Page();
        }

        await using var stream = Input.File.OpenReadStream();
        var command = new ImportFileCommand(Input.Source, Input.MarketplaceId, Input.File.FileName, Input.File.Length, stream, User.Identity!.Name!);

        var result = await importService.ImportAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return Page();
        }

        TempData[StatusMessage.Success] = $"Staged {result.Value.RowCount:N0} rows as batch {result.Value.BatchId}. Review and promote it below.";
        return RedirectToPage("/Tools/Batches/Details", new { id = result.Value.BatchId });
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
        /// <summary>Report type the file contains.</summary>
        [Required]
        [Display(Name = "Report type")]
        public ImportSource Source { get; set; } = ImportSource.Orders;

        /// <summary>Marketplace the file's data belongs to.</summary>
        [Required]
        [Display(Name = "Marketplace")]
        public string MarketplaceId { get; set; } = string.Empty;

        /// <summary>The uploaded file.</summary>
        [Required(ErrorMessage = "Choose a file to import.")]
        [Display(Name = "File")]
        public IFormFile? File { get; set; }
    }
}

/// <summary>Request-size constants for the import page (attribute arguments must be constants).</summary>
internal static class ImportPageLimits
{
    /// <summary>25 MB: the 20 MB default file limit plus multipart headroom.</summary>
    public const long MaxRequestBytes = 25L * 1024 * 1024;
}
