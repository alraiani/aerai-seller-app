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

    /// <summary>
    /// Marks the marketplace's stored held transactions that Amazon no longer lists as held as released
    /// (<c>DEFERRED_RELEASED</c>). A held sale keeps its posted date when Amazon releases it, so no later window
    /// would read it again.
    /// </summary>
    /// <param name="marketplaceId">Marketplace whose schedule read the held listing.</param>
    /// <param name="stillHeld">Every id in the complete held listing just read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many were marked released.</returns>
    Task<int> MarkReleasedAsync(string marketplaceId, IReadOnlyCollection<string> stillHeld, CancellationToken cancellationToken);
}
