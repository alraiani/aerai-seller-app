namespace AERai.Web.Domain.Core;

/// <summary>
/// One entry in Amazon's transaction ledger (Finances API <c>listTransactions</c>): a shipment's payment, a
/// refund, a fee, a reimbursement, and so on, as Amazon posted it. Written by the financial transactions sync;
/// the profit &amp; loss page reads it by posted date, and it adds up to the settlements it was paid in.
/// </summary>
/// <remarks>
/// Settlements are built from these same transactions, but a transaction is posted (and listed here) within
/// about a day, held or not, while it only reaches a settlement when Amazon pays it. Each row's
/// <see cref="Lines"/> add up to its <see cref="Amount"/> exactly.
/// </remarks>
public sealed class FinancialTransaction
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>Marketplace whose schedule pulled the transaction (the request names it, so this is the sale's marketplace).</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Amazon's transaction identifier (unique per marketplace).</summary>
    public required string TransactionId { get; set; }

    /// <summary>Amazon's transaction type, e.g. <c>Shipment</c>, <c>Refund</c>, <c>ServiceFee</c>.</summary>
    public string? TransactionType { get; set; }

    /// <summary>Amazon's description, e.g. <c>Order Payment</c>.</summary>
    public string? Description { get; set; }

    /// <summary>Amazon's status when last read, e.g. <c>RELEASED</c>, <c>DEFERRED</c>, <c>DEFERRED_RELEASED</c>.</summary>
    public string? Status { get; set; }

    /// <summary>When Amazon posted it: the day it counts in profit.</summary>
    public DateTimeOffset PostedDate { get; set; }

    /// <summary>The order it belongs to (related identifier <c>ORDER_ID</c>), when any.</summary>
    public string? AmazonOrderId { get; set; }

    /// <summary>The settlement it belongs to (related identifier <c>SETTLEMENT_ID</c>), when Amazon gives one.</summary>
    public string? SettlementId { get; set; }

    /// <summary>Its total, signed (money to the seller is positive).</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency of <see cref="Amount"/>.</summary>
    public required string Currency { get; set; }

    /// <summary>When the sync last wrote it.</summary>
    public DateTimeOffset SyncedAt { get; set; }

    /// <summary>Its amounts, one per breakdown, adding up to <see cref="Amount"/>.</summary>
    public List<FinancialTransactionLine> Lines { get; set; } = [];
}
