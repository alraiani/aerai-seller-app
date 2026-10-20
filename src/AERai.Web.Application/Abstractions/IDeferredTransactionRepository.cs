using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Abstractions;

/// <summary>Writes Amazon's deferred transactions to <c>core.DeferredTransaction</c>.</summary>
public interface IDeferredTransactionRepository
{
    /// <summary>
    /// Records one complete listing for a marketplace, in one transaction: every transaction Amazon still
    /// holds is added or refreshed, and any transaction stored as deferred that Amazon no longer lists is
    /// marked released (at its maturity date when that has passed, otherwise at <paramref name="seenAt"/>).
    /// </summary>
    /// <param name="marketplaceId">Marketplace whose schedule pulled the transactions.</param>
    /// <param name="stillDeferred">Every transaction Amazon still holds (the complete listing).</param>
    /// <param name="seenAt">When the listing was read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many are held now, and how many were newly marked released.</returns>
    Task<DeferredApplyResult> ApplyAsync(
        string marketplaceId,
        IReadOnlyCollection<DeferredTransactionItem> stillDeferred,
        DateTimeOffset seenAt,
        CancellationToken cancellationToken);
}
