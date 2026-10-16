using AERai.Web.Application.Common;

namespace AERai.Web.Application.Orders;

/// <summary>What the Orders page shows: one page of orders, the totals over all pages, and the statuses to filter by.</summary>
/// <param name="Orders">The requested page.</param>
/// <param name="Totals">Totals over every order the search and filters select.</param>
/// <param name="Statuses">Every status the marketplace's orders have, most orders first.</param>
public sealed record OrderList(PagedResult<OrderListItem> Orders, OrderListTotals Totals, IReadOnlyList<string> Statuses);
