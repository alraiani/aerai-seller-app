using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Abstractions;

/// <summary>Writes Amazon's transaction ledger to <c>core.FinancialTransaction</c>.</summary>
public interface IFinancialTransactionRepository
{
    /// <summary>
    /// Inserts new transactions and updates known ones (by marketplace and Amazon's id), replacing their lines,
    /// in one transaction. Lines don't reference the catalog, so a SKU Amazon charges for that isn't in it still counts.
    /// </summary>
    /// <param name="marketplaceId">Marketplace whose schedule read them.</param>
    /// <param name="transactions">The transactions, one per id.</param>
    /// <param name="syncedAt">When they were read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many were new and how many updated.</returns>
    Task<(int Inserted, int Updated)> UpsertAsync(
        string marketplaceId, IReadOnlyCollection<AmazonFinancialTransaction> transactions, DateTimeOffset syncedAt, CancellationToken cancellationToken);
}
