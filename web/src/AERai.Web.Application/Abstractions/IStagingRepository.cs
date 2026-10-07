using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Persists raw import batches into the <c>stg</c> schema.
/// </summary>
public interface IStagingRepository
{
    /// <summary>
    /// Saves a new batch together with all of its staging rows in one transaction.
    /// </summary>
    /// <param name="batch">A batch that has not been saved yet.</param>
    /// <returns>The database-generated batch id.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<long> AddBatchAsync(ImportBatch batch, CancellationToken cancellationToken);
}
