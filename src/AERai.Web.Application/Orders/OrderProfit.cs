namespace AERai.Web.Application.Orders;

/// <summary>
/// Estimated profit on one order: what Amazon paid out for it less the cost of the goods sold.
/// </summary>
/// <remarks>
/// Only paid-out orders get a number. Before payout Amazon's fees aren't known, and sales less cost alone
/// would overstate the profit by every fee. Cost uses the SKU's current cost in the marketplace, so a
/// cost changed after the sale changes the estimate.
/// </remarks>
/// <param name="State">Whether the profit is known.</param>
/// <param name="CostOfGoods">Units × unit cost over every item, or <see langword="null"/> when any item has no cost.</param>
/// <param name="Profit">Net paid out less <paramref name="CostOfGoods"/>, or <see langword="null"/> unless <see cref="OrderProfitState.Estimated"/>.</param>
public sealed record OrderProfit(OrderProfitState State, decimal? CostOfGoods, decimal? Profit)
{
    /// <summary>Estimates the profit on an order.</summary>
    /// <param name="status">The order's status.</param>
    /// <param name="items">The order's items.</param>
    /// <param name="payout">What has been paid out for it, or <see langword="null"/> when nothing yet.</param>
    /// <returns>The estimate.</returns>
    public static OrderProfit Estimate(string status, IReadOnlyCollection<OrderItemRow> items, OrderPayout? payout)
    {
        ArgumentNullException.ThrowIfNull(items);

        decimal? cost = items.Any(i => i.UnitCost is null) ? null : items.Sum(i => i.Quantity * i.UnitCost!.Value);
        if (OrderStatuses.IsCancelled(status))
        {
            return new OrderProfit(OrderProfitState.Cancelled, cost, null);
        }

        if (payout is null)
        {
            return new OrderProfit(OrderProfitState.NotPaidOut, cost, null);
        }

        // An order without items has nothing to cost, so it isn't "missing" a cost.
        return cost is { } known
            ? new OrderProfit(OrderProfitState.Estimated, known, payout.Net - known)
            : new OrderProfit(OrderProfitState.CostMissing, null, null);
    }
}
