using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Orders;

/// <summary>The Orders page: orders with their items, payouts, and estimated profit, and the CSV export.</summary>
public interface IOrderService
{
    /// <summary>Loads one page of orders, the totals over all pages, and the statuses to filter by.</summary>
    /// <param name="marketplace">Marketplace to report on.</param>
    /// <param name="request">Paging and search (order id, or an item's SKU, ASIN, or title).</param>
    /// <param name="filter">Purchase dates, status, and sort.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The list.</returns>
    Task<OrderList> GetListAsync(Marketplace marketplace, PageRequest request, OrderListFilter filter, CancellationToken cancellationToken);

    /// <summary>Writes every order the search and filter select as a CSV, one row per item.</summary>
    /// <param name="marketplace">Marketplace to report on.</param>
    /// <param name="request">Search as for <see cref="GetListAsync"/>; paging is ignored.</param>
    /// <param name="filter">Purchase dates, status, and sort.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The file.</returns>
    Task<OrderExport> ExportAsync(Marketplace marketplace, PageRequest request, OrderListFilter filter, CancellationToken cancellationToken);
}
