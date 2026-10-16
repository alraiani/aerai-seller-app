using AERai.Web.Application.Common;
using AERai.Web.Application.Orders;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Abstractions;

/// <summary>Read-only queries behind the Orders page. They return rows; <see cref="IOrderService"/> combines them.</summary>
public interface IOrderQueries
{
    /// <summary>Lists orders.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="request">Paging; every word of the search (see <see cref="Products.ProductSearch"/>) must match the order id or an item's SKU, ASIN, or title.</param>
    /// <param name="filter">Purchase dates, status, and sort.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One page of orders.</returns>
    Task<PagedResult<OrderSummary>> GetOrdersAsync(string marketplaceId, PageRequest request, OrderListFilter filter, CancellationToken cancellationToken);

    /// <summary>Totals over every order the same search and filter select, across all pages.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="request">Search as for <see cref="GetOrdersAsync"/>; paging is ignored.</param>
    /// <param name="filter">Purchase dates and status; the sort is ignored.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The totals.</returns>
    Task<OrderListTotals> GetTotalsAsync(string marketplaceId, PageRequest request, OrderListFilter filter, CancellationToken cancellationToken);

    /// <summary>Every status the marketplace's orders have, for the status filter.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The statuses, most orders first.</returns>
    Task<IReadOnlyList<string>> GetStatusesAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>The items on some orders, with each SKU's catalog details and cost in the marketplace.</summary>
    /// <param name="marketplaceId">The orders' marketplace (whose costs apply).</param>
    /// <param name="amazonOrderIds">The orders.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The items, by order then SKU.</returns>
    Task<IReadOnlyList<OrderItemRow>> GetItemsAsync(string marketplaceId, IReadOnlyCollection<string> amazonOrderIds, CancellationToken cancellationToken);

    /// <summary>What each settlement holds for some orders, as paid.</summary>
    /// <param name="settlementMarketplaceId">
    /// The marketplace whose settlements list holds the orders' payouts (a Canadian order's payout is
    /// listed with the United States: <see cref="Domain.Core.Marketplace.SettlementMarketplaceId"/>).
    /// </param>
    /// <param name="amazonOrderIds">The orders.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One row per order, settlement, and currency.</returns>
    Task<IReadOnlyList<OrderSettlementAmounts>> GetSettlementAmountsAsync(string settlementMarketplaceId, IReadOnlyCollection<string> amazonOrderIds, CancellationToken cancellationToken);

    /// <summary>The items of every order the search and filter select, for the CSV.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="request">Search as for <see cref="GetOrdersAsync"/>; paging is ignored.</param>
    /// <param name="filter">Purchase dates, status, and sort (the rows follow the list's order).</param>
    /// <param name="maxRows">Return at most this many rows.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows, by order (in the list's order) then SKU.</returns>
    Task<IReadOnlyList<OrderExportRow>> GetExportRowsAsync(string marketplaceId, PageRequest request, OrderListFilter filter, int maxRows, CancellationToken cancellationToken);
}
