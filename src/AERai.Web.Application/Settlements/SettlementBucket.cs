namespace AERai.Web.Application.Settlements;

/// <summary>
/// The column a settlement amount counts toward, as decided by <c>rpt.vw_SettlementLine</c>
/// (the only place that rule lives). They are the columns of Seller Central's statement.
/// </summary>
public enum SettlementBucket
{
    /// <summary>Item price, shipping, and tax on orders (less the tax Amazon withholds), and FBA inventory reimbursements.</summary>
    Sales = 1,

    /// <summary>Everything Amazon charged: fees, promotions, advertising, subscriptions, clawbacks.</summary>
    Expenses = 2,

    /// <summary>Refund transactions.</summary>
    Refunds = 3,

    /// <summary>Account level reserve: held back from the payout (negative) or released from an earlier one (positive).</summary>
    Reserve = 4,
}
