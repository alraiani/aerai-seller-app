using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Moves staged rows into the curated <c>core</c> tables.
/// </summary>
public interface IPromotionService
{
    /// <summary>
    /// Promotes every valid row in a staging batch into the curated <c>core</c> tables.
    /// </summary>
    /// <param name="batchId">The batch to promote.</param>
    /// <returns>A summary with promoted/rejected row counts, or a failure when the batch does not exist.</returns>
    /// <remarks>
    /// Idempotent: re-promoting a batch upserts by natural key (orders, inventory) or replaces whole
    /// settlements, so it never creates duplicates. Rows that cannot be converted are skipped and
    /// keep an error message; they never fail the batch.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result<PromotionSummary>> PromoteAsync(long batchId, CancellationToken cancellationToken);
}
