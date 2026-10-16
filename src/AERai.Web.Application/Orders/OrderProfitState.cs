namespace AERai.Web.Application.Orders;

/// <summary>Whether an order's profit can be estimated, and if not, why.</summary>
public enum OrderProfitState
{
    /// <summary>Paid out, and every item has a cost: the profit is known.</summary>
    Estimated = 0,

    /// <summary>Not in any settlement yet, so Amazon's fees aren't known.</summary>
    NotPaidOut = 1,

    /// <summary>At least one item has no cost of goods in this marketplace.</summary>
    CostMissing = 2,

    /// <summary>The order was cancelled.</summary>
    Cancelled = 3,
}
