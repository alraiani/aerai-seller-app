namespace AERai.Web.Domain.Reporting;

/// <summary>
/// The finest column a settlement line counts toward, as decided by <c>rpt.vw_SettlementLine</c>.
/// Refunds split into <see cref="RefundedSales"/> and <see cref="RefundedExpenses"/>; expenses into
/// <see cref="Shipping"/>, <see cref="FbaFees"/>, <see cref="Commission"/>, and <see cref="OtherExpenses"/>.
/// </summary>
/// <remarks>Stored by name in the view, so the names must match the view's category values.</remarks>
public enum SettlementCategory
{
    /// <summary>Item price, shipping, and tax on orders, and FBA inventory reimbursements.</summary>
    Sales = 1,

    /// <summary>Price, shipping, and tax given back on refunds.</summary>
    RefundedSales = 2,

    /// <summary>Fees given back or charged on refunds (commission, refund commission, shipping chargebacks, promotions).</summary>
    RefundedExpenses = 3,

    /// <summary>Shipping to customers and into Amazon.</summary>
    Shipping = 4,

    /// <summary>Every FBA fee: fulfillment, storage, placement, returns, removals, AWD.</summary>
    FbaFees = 5,

    /// <summary>Amazon's referral fees.</summary>
    Commission = 6,

    /// <summary>Every other charge: advertising, subscriptions, deals, promotions, clawbacks, deductions.</summary>
    OtherExpenses = 7,

    /// <summary>Account level reserve held back or released.</summary>
    Reserve = 8,
}
