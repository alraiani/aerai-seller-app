using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Orders;

/// <summary>One order on the Orders list with its items, what was paid out for it, and its profit.</summary>
/// <param name="Order">The order's totals (from <c>rpt.vw_OrderSummary</c>).</param>
/// <param name="Items">The order's items, by SKU.</param>
/// <param name="Payout">What Amazon has paid out for it, or <see langword="null"/> when it isn't in a settlement yet.</param>
/// <param name="Profit">The estimated profit.</param>
/// <param name="Deferral">
/// The order's payout Amazon still holds (its deferred transaction), when it is not paid out yet and Amazon
/// deferred it; otherwise <see langword="null"/>.
/// </param>
public sealed record OrderListItem(OrderSummary Order, IReadOnlyList<OrderItemRow> Items, OrderPayout? Payout, OrderProfit Profit, DeferredPaymentRow? Deferral = null);
