using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Tools.Batches;

/// <summary>
/// One staging batch: its status, the rows promotion rejected, and the Promote action.
/// </summary>
/// <param name="batches">Batch queries.</param>
/// <param name="promotion">Promotion use case.</param>
/// <param name="importService">Staging use cases (re-stage from the raw file).</param>
public sealed class DetailsModel(IImportBatchQueries batches, IPromotionService promotion, IStagingImportService importService) : ListPageModel
{
    /// <summary>The batch.</summary>
    public ImportBatchSummary Batch { get; private set; } = default!;

    /// <summary>The current page of rejected rows.</summary>
    public PagedResult<RejectedRow> Rejected { get; private set; } = default!;

    /// <summary>Loads the batch and its rejected rows.</summary>
    /// <param name="id">Batch id from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or 404 when the batch does not exist.</returns>
    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken)
    {
        var batch = await batches.GetAsync(id, cancellationToken);
        if (batch is null)
        {
            return NotFound();
        }

        Batch = batch;
        Rejected = await batches.GetRejectedRowsAsync(id, ToPageRequest(), cancellationToken);
        return Page();
    }

    /// <summary>Promotes the batch into the curated tables (Post/Redirect/Get).</summary>
    /// <param name="id">Batch id from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to this batch with a status message.</returns>
    public async Task<IActionResult> OnPostPromoteAsync(long id, CancellationToken cancellationToken)
    {
        var result = await promotion.PromoteAsync(id, cancellationToken);
        if (result.IsSuccess)
        {
            TempData[StatusMessage.Success] =
                $"Promoted {result.Value.PromotedRowCount:N0} row(s); {result.Value.RejectedRowCount:N0} rejected" +
                (result.Value.SkippedRowCount > 0 ? $"; {result.Value.SkippedRowCount:N0} for other marketplaces." : ".");
        }
        else
        {
            TempData[StatusMessage.Error] = result.Error;
        }

        return RedirectToPage(new { id });
    }

    /// <summary>
    /// Re-parses this batch's stored raw file into a new batch (Post/Redirect/Get). The original
    /// batch is left untouched.
    /// </summary>
    /// <param name="id">Batch id from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the new batch, or back to this one with an error.</returns>
    public async Task<IActionResult> OnPostRestageAsync(long id, CancellationToken cancellationToken)
    {
        var result = await importService.RestageAsync(id, User.Identity!.Name!, cancellationToken);
        if (result.IsFailure)
        {
            TempData[StatusMessage.Error] = result.Error;
            return RedirectToPage(new { id });
        }

        TempData[StatusMessage.Success] = $"Re-staged batch {id} from its raw file as batch {result.Value.BatchId} ({result.Value.RowCount:N0} rows).";
        return RedirectToPage(new { id = result.Value.BatchId });
    }
}
