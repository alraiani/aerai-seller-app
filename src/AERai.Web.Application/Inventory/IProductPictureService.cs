using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>Bulk ways to give SKUs a picture: pull Amazon's listing pictures, or upload many files at once.</summary>
public interface IProductPictureService
{
    /// <summary>Counts SKUs with and without a picture.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The coverage.</returns>
    Task<PictureCoverage> GetCoverageAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gives SKUs that have an ASIN but no picture their Amazon listing's main picture. SKUs that
    /// already have one are never touched, so manual uploads always win.
    /// </summary>
    /// <param name="marketplace">Marketplace whose catalog to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Per-ASIN outcomes and how many SKUs are left for another pull, or why Amazon couldn't be reached.</returns>
    Task<Result<PictureImportResult>> PullFromAmazonAsync(Marketplace marketplace, CancellationToken cancellationToken);

    /// <summary>
    /// Stores uploaded pictures (single files or .zip archives), matching each file name to a SKU,
    /// or to an ASIN for all of its SKUs. Existing pictures are replaced.
    /// </summary>
    /// <param name="files">The uploaded files.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Per-file outcomes, or why the upload as a whole was refused.</returns>
    Task<Result<PictureImportResult>> UploadAsync(IReadOnlyList<PictureFile> files, CancellationToken cancellationToken);
}
