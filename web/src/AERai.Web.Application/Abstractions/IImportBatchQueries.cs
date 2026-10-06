using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Read-side queries over staging batches for the import tools and dashboard.
/// </summary>
public interface IImportBatchQueries
{
    /// <summary>Lists one marketplace's batches, newest first.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="request">Paging; <see cref="PageRequest.Search"/> filters by file name.</param>
    /// <returns>One page of batches.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<PagedResult<ImportBatchSummary>> ListAsync(string marketplaceId, PageRequest request, CancellationToken cancellationToken);

    /// <summary>Gets one batch.</summary>
    /// <param name="batchId">Batch id.</param>
    /// <returns>The batch, or <see langword="null"/> when it does not exist.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<ImportBatchSummary?> GetAsync(long batchId, CancellationToken cancellationToken);

    /// <summary>Gets the most recently uploaded batch.</summary>
    /// <returns>The batch, or <see langword="null"/> when nothing has been imported yet.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<ImportBatchSummary?> GetLatestAsync(CancellationToken cancellationToken);

    /// <summary>Lists the rows promotion rejected in a batch, in file order.</summary>
    /// <param name="batchId">Batch id.</param>
    /// <param name="request">Paging.</param>
    /// <returns>One page of rejected rows.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<PagedResult<RejectedRow>> GetRejectedRowsAsync(long batchId, PageRequest request, CancellationToken cancellationToken);
}
