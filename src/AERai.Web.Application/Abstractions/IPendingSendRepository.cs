using AERai.Web.Application.Inventory;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Reads and closes sends pending to Amazon: <c>core.HomeStockMovement</c> entries of type Shipped to
/// Amazon that haven't been reversed. Only the reconciliation fields and a missing reference are ever
/// written; units and balances stay as logged.
/// </summary>
public interface IPendingSendRepository
{
    /// <summary>A marketplace's open sends, oldest first.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The open sends.</returns>
    Task<IReadOnlyList<PendingSend>> ListOpenAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>Every send (open or not) of the given SKUs logged at or after a point in time, oldest first.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="skus">The SKUs.</param>
    /// <param name="since">Earliest logging time included.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The sends.</returns>
    Task<IReadOnlyList<PendingSend>> ListSinceAsync(string marketplaceId, IReadOnlyCollection<string> skus, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>One send, open or not.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="id">The send's ledger entry id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The send, or <see langword="null"/> when there is no such send (or it was reversed).</returns>
    Task<PendingSend?> GetAsync(string marketplaceId, long id, CancellationToken cancellationToken);

    /// <summary>
    /// Records new reconciled totals on sends that are still open; a send closed in the meantime (by a
    /// user, or another instance) is left as it is.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="matches">The new totals.</param>
    /// <param name="at">When they were matched (stored on the sends that close).</param>
    /// <param name="by">Who matched them: a user's email, or the system reconciler.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many sends were updated.</returns>
    Task<int> ReconcileAsync(string marketplaceId, IReadOnlyList<PendingSendMatch> matches, DateTimeOffset at, string by, CancellationToken cancellationToken);

    /// <summary>Sets the reference of an open send that has none yet.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="id">The send's ledger entry id.</param>
    /// <param name="reference">The FBA shipment id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="false"/> when the send isn't open or already has a reference.</returns>
    Task<bool> SetReferenceAsync(string marketplaceId, long id, string reference, CancellationToken cancellationToken);
}
