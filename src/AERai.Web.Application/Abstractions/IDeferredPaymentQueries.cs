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
    /// <param name="filter">View, sort, and settlement.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page.</returns>
    Task<PagedResult<DeferredPaymentRow>> GetPageAsync(string marketplaceId, PageRequest request, DeferredListFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// A page of a marketplace's deferred transactions grouped by the settlement they were sold in, newest first:
    /// by period end, or for a settlement not imported (the open one) by its latest sale.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="listMarketplaceId">The marketplace whose settlements list holds this marketplace's payouts.</param>
    /// <param name="request">Page (of groups) and search (every word must match a transaction's order id or description).</param>
    /// <param name="view">Still held, released, or both.</param>
    /// <param name="maxRowsPerGroup">At most this many transactions in each group's rows, soonest release first.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page of groups.</returns>
    Task<PagedResult<DeferredSettlementGroup>> GetGroupsAsync(
        string marketplaceId, string listMarketplaceId, PageRequest request, DeferredView view, int maxRowsPerGroup, CancellationToken cancellationToken);

    /// <summary>What Amazon deferred out of some settlements, per settlement and currency.</summary>
    /// <param name="marketplaceIds">Marketplaces whose payouts the settlements list holds.</param>
    /// <param name="settlementIds">Settlements.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One row per settlement and currency with deferrals.</returns>
    Task<IReadOnlyList<SettlementDeferral>> GetBySettlementsAsync(
        IReadOnlyCollection<string> marketplaceIds, IReadOnlyCollection<string> settlementIds, CancellationToken cancellationToken);

    /// <summary>What Amazon deferred out of a settlement, per SKU and price each.</summary>
    /// <param name="marketplaceIds">Marketplaces whose payouts the settlements list holds.</param>
    /// <param name="settlementId">Settlement.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One row per SKU, price each, and currency.</returns>
    Task<IReadOnlyList<SkuDeferral>> GetSkuDeferralsAsync(IReadOnlyCollection<string> marketplaceIds, string settlementId, CancellationToken cancellationToken);

    /// <summary>The deferred products of one SKU at one price each in a settlement, one per order item.</summary>
    /// <param name="marketplaceIds">Marketplaces whose payouts the settlements list holds.</param>
    /// <param name="settlementId">Settlement.</param>
    /// <param name="sku">SKU, or <see langword="null"/> for products without one.</param>
    /// <param name="priceEach">Price each (as on <see cref="SkuDeferral.PriceEach"/>), or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The products, soonest release first.</returns>
    Task<IReadOnlyList<DeferredLineItem>> GetSkuLinesAsync(
        IReadOnlyCollection<string> marketplaceIds, string settlementId, string? sku, decimal? priceEach, CancellationToken cancellationToken);

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
