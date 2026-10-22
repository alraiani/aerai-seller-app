using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Abstractions;

/// <summary>Writes Amazon's deferred transactions to <c>core.DeferredTransaction</c>.</summary>
public interface IDeferredTransactionRepository
{
    /// <summary>
    /// Records one complete pull for a marketplace, in one transaction: every transaction Amazon still holds is
    /// added or refreshed as deferred, every released one is added or refreshed as released, and any transaction
    /// stored as deferred that is in neither listing is marked released. A release is dated by the maturity date
    /// when that has passed, otherwise by <paramref name="seenAt"/>. Each transaction's products are replaced.
    /// </summary>
    /// <param name="marketplaceId">Marketplace whose schedule pulled the transactions.</param>
    /// <param name="stillDeferred">Every transaction Amazon still holds (the complete listing).</param>
    /// <param name="released">Transactions Amazon deferred and released, within the run's window.</param>
    /// <param name="seenAt">When the listing was read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many are held now, how many released ones were listed, and how many were newly marked released.</returns>
    Task<DeferredApplyResult> ApplyAsync(
        string marketplaceId,
        IReadOnlyCollection<AmazonDeferredTransaction> stillDeferred,
        IReadOnlyCollection<AmazonDeferredTransaction> released,
        DateTimeOffset seenAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the marketplace already has released transactions that Amazon listed (they carry the settlement
    /// they were sold in), i.e. whether its release history has been read once.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> once a run has listed releases.</returns>
    Task<bool> HasReleaseHistoryAsync(string marketplaceId, CancellationToken cancellationToken);
}
