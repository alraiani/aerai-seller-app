namespace AERai.Web.Application.Settlements;

/// <summary>
/// The column a settlement amount counts toward, as decided by <c>rpt.vw_SettlementLine</c>
/// (the only place that rule lives).
/// </summary>
public enum SettlementBucket
{
    /// <summary>Item price amounts on order transactions.</summary>
    Sales = 1,

    /// <summary>Selling and fulfillment fees.</summary>
    Fees = 2,

    /// <summary>Refund transactions.</summary>
    Refunds = 3,

    /// <summary>Everything else: promotions, reimbursements, adjustments.</summary>
    Other = 4,
}
