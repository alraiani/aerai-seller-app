using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Inventory;

/// <summary>
/// Gives many SKUs a picture at once: pull each listing's main picture from Amazon (only for SKUs
/// without one), or upload pictures and .zip files named after their SKU or ASIN. Operators and
/// Admins only.
/// </summary>
/// <param name="pictures">Picture imports.</param>
/// <param name="currentMarketplace">The marketplace whose catalog an Amazon pull reads.</param>
/// <param name="connection">Whether Amazon can be called.</param>
[Authorize(Policy = AppPolicies.RequireOperator)]
[RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
[RequestSizeLimit(MaxUploadBytes)]
public sealed class PicturesModel(IProductPictureService pictures, ICurrentMarketplace currentMarketplace, IAmazonConnectionInfo connection) : PageModel
{
    /// <summary>Largest upload accepted (all files together); a few hundred product photos fit easily.</summary>
    public const int MaxUploadBytes = 200 * 1024 * 1024;

    /// <summary>Marketplace whose catalog an Amazon pull reads.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>How many SKUs have a picture.</summary>
    public PictureCoverage Coverage { get; private set; } = new(0, 0, 0, 0);

    /// <summary>Whether Amazon can be called, and why not.</summary>
    public IAmazonConnectionInfo Connection => connection;

    /// <summary>The uploaded pictures and .zip files.</summary>
    [BindProperty]
    public List<IFormFile> Files { get; set; } = [];

    /// <summary>What the last pull or upload did, shown on the response that ran it.</summary>
    public PictureImportResult? Result { get; private set; }

    /// <summary>Which action produced <see cref="Result"/>.</summary>
    public bool ResultIsFromAmazon { get; private set; }

    /// <summary>Shows coverage and both forms.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public Task OnGetAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    /// <summary>Pulls Amazon's main listing picture for SKUs that have an ASIN and no picture.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page with the outcome.</returns>
    public async Task<IActionResult> OnPostAmazonAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        ModelState.Remove(nameof(Files));

        var result = await pictures.PullFromAmazonAsync(Marketplace, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
        }
        else
        {
            // Rendered on this response rather than redirected: the per-ASIN list is too large for TempData.
            (Result, ResultIsFromAmazon) = (result.Value, true);
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    /// <summary>Stores the uploaded pictures, matched to SKUs by file name.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page with the outcome per file.</returns>
    public async Task<IActionResult> OnPostUploadAsync(CancellationToken cancellationToken)
    {
        var files = Files.Where(f => f.Length > 0).ToList();
        var streams = files.Select(f => f.OpenReadStream()).ToList();
        try
        {
            var result = await pictures.UploadAsync(files.Select((f, i) => new PictureFile(f.FileName, streams[i])).ToList(), cancellationToken);
            if (result.IsFailure)
            {
                ModelState.AddModelError(nameof(Files), result.Error);
            }
            else
            {
                Result = result.Value;
            }
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    /// <summary>Pill style for a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The CSS classes.</returns>
    public static string PillClass(PictureImportStatus status) => status switch
    {
        PictureImportStatus.Added or PictureImportStatus.Replaced => "pill pill-ok",
        PictureImportStatus.NoMatch or PictureImportStatus.Duplicate or PictureImportStatus.NotOnAmazon => "pill pill-warn",
        _ => "pill pill-bad",
    };

    /// <summary>Label for a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The label.</returns>
    public static string Label(PictureImportStatus status) => status switch
    {
        PictureImportStatus.Added => "Added",
        PictureImportStatus.Replaced => "Replaced",
        PictureImportStatus.NoMatch => "No match",
        PictureImportStatus.Duplicate => "Duplicate",
        PictureImportStatus.Rejected => "Rejected",
        PictureImportStatus.NotOnAmazon => "No Amazon picture",
        _ => "Failed",
    };

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Coverage = await pictures.GetCoverageAsync(cancellationToken);
    }
}
