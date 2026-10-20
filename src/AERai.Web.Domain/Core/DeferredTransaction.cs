namespace AERai.Web.Domain.Core;

/// <summary>
/// A sale (or other transaction) whose payout Amazon deferred: held back under its delivery-date policy
/// (<c>DD7</c>: until seven days after delivery) or until an invoiced business order is paid (<c>B2B</c>).
/// One row per Amazon transaction, written by the deferred transactions sync from the Finances API.
/// </summary>
/// <remarks>
/// A deferred transaction is in no settlement report until Amazon releases it; it then arrives as ordinary
/// order lines in a later settlement. These rows show what is still held (and so not yet in the bank)
/// and which settled orders were held before they were paid.
/// </remarks>
public sealed class DeferredTransaction
{
    /// <summary>Amazon's transaction identifier (the key).</summary>
    public required string TransactionId { get; set; }

    /// <summary>Marketplace whose schedule pulled the transaction.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>The Amazon order the transaction belongs to, when it has one.</summary>
    public string? AmazonOrderId { get; set; }

    /// <summary>Amazon's transaction type, e.g. <c>Shipment</c>.</summary>
    public string? TransactionType { get; set; }

    /// <summary>Amazon's description, e.g. <c>Order Payment</c>.</summary>
    public string? Description { get; set; }

    /// <summary>When Amazon posted the transaction.</summary>
    public DateTimeOffset? PostedDate { get; set; }

    /// <summary>The transaction's total: what will be paid out once released (signed).</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency of <see cref="Amount"/>.</summary>
    public string? Currency { get; set; }

    /// <summary>Amazon's deferral policy, e.g. <c>DD7</c> (delivery date) or <c>B2B</c> (invoiced order).</summary>
    public string? DeferralReason { get; set; }

    /// <summary>When Amazon will release (or released) the money.</summary>
    public DateTimeOffset? MaturityDate { get; set; }

    /// <summary>Whether the money is still held.</summary>
    public DeferredTransactionStatus Status { get; set; } = DeferredTransactionStatus.Deferred;

    /// <summary>When the sync first saw the transaction.</summary>
    public DateTimeOffset FirstSeenAt { get; set; }

    /// <summary>When a sync last saw the transaction in Amazon's listing.</summary>
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>
    /// When it was released: its maturity date once that has passed, otherwise when a sync first found it
    /// gone from Amazon's deferred listing.
    /// </summary>
    public DateTimeOffset? ReleasedAt { get; set; }
}
