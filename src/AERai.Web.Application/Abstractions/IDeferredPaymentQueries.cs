using AERai.Web.Application.Common;
using AERai.Web.Application.Settlements;

namespace AERai.Web.Application.Abstractions;

/// <summary>Reads <c>core.DeferredTransaction</c> and the deferred sync's health. Rows only; rules live in <see cref="IDeferredPaymentService"/>.</summary>
public interface IDeferredPaymentQueries
{
    /// <summary>What Amazon holds for a marketplace, per currency.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One row per currency with held transactions.</returns>
    Task<IReadOnlyList<DeferredHeld>> GetHeldAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>Whether the marketplace's deferred transactions are synced.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The sync's state.</returns>
    Task<DeferredSyncState> GetSyncStateAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>A page of a marketplace's deferred transactions.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="request">Page and search (every word must match the order id or description).</param>
    /// <param name="filter">View and sort.</param>
    /// <param name="releasedSince">For the released view: only transactions released at or after this instant.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page.</returns>
    Task<PagedResult<DeferredPaymentRow>> GetPageAsync(
        string marketplaceId, PageRequest request, DeferredListFilter filter, DateTimeOffset releasedSince, CancellationToken cancellationToken);

    /// <summary>
    /// The released deferred transactions of the orders in one settlement. Read for every marketplace whose
    /// payouts the settlement list holds (Canada's are in the United States list).
    /// </summary>
    /// <param name="listMarketplaceId">The marketplace whose settlements list holds the settlement.</param>
    /// <param name="settlementId">Settlement.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The transactions, any order.</returns>
    Task<IReadOnlyList<DeferredPaymentRow>> GetReleasedInSettlementAsync(string listMarketplaceId, string settlementId, CancellationToken cancellationToken);

    /// <summary>The deferred transactions (held or released) of some orders.</summary>
    /// <param name="marketplaceIds">Marketplaces the orders may belong to.</param>
    /// <param name="orderIds">Amazon order ids.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The transactions, any order.</returns>
    Task<IReadOnlyList<DeferredPaymentRow>> GetByOrdersAsync(IReadOnlyCollection<string> marketplaceIds, IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken);
}
