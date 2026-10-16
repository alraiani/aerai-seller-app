namespace AERai.Web.Application.Orders;

/// <summary>
/// Totals over the orders an Orders list filter selects (every page, not just the one shown). Cancelled
/// orders are counted but left out of units and sales, as in the sales views.
/// </summary>
/// <param name="Orders">Orders matched, cancelled included.</param>
/// <param name="Cancelled">Of those, how many were cancelled.</param>
/// <param name="Units">Units on the orders that weren't cancelled.</param>
/// <param name="Sales">Item sales on the orders that weren't cancelled, in the marketplace's currency.</param>
public sealed record OrderListTotals(int Orders, int Cancelled, int Units, decimal Sales)
{
    /// <summary>No orders.</summary>
    public static OrderListTotals Empty { get; } = new(0, 0, 0, 0);

    /// <summary>Average sales per order that wasn't cancelled, or <see langword="null"/> when there are none.</summary>
    public decimal? AverageOrderValue => Orders - Cancelled > 0 ? Sales / (Orders - Cancelled) : null;
}
