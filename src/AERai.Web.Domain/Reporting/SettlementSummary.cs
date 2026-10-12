namespace AERai.Web.Domain.Reporting;

/// <summary>
/// Read model for <c>rpt.vw_SettlementSummary</c>: one row per settlement with amounts bucketed
/// into sales, fees, refunds, and other.
/// </summary>
public sealed class SettlementSummary
{
    /// <summary>Amazon settlement identifier.</summary>
    public required string SettlementId { get; set; }

    /// <summary>Marketplace the payout settles.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Start of the settlement period.</summary>
    public DateTimeOffset? PeriodStart { get; set; }

    /// <summary>End of the settlement period.</summary>
    public DateTimeOffset? PeriodEnd { get; set; }

    /// <summary>ISO 4217 currency code.</summary>
    public string? Currency { get; set; }

    /// <summary>Item price amounts (principal, shipping, tax) on order transactions.</summary>
    public decimal Sales { get; set; }

    /// <summary>Selling and fulfillment fees (negative).</summary>
    public decimal Fees { get; set; }

    /// <summary>Net of refund transactions (usually negative).</summary>
    public decimal Refunds { get; set; }

    /// <summary>Everything else: promotions, reimbursements, adjustments.</summary>
    public decimal Other { get; set; }

    /// <summary>Sum of all amounts — the payout.</summary>
    public decimal Net { get; set; }
}
