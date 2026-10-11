namespace AERai.Web.Domain.Core;

/// <summary>
/// One signed amount in a <see cref="Settlement"/> (a sale, fee, refund, reimbursement, etc.).
/// </summary>
/// <remarks>
/// Settlement files have no per-line natural key, so promotion replaces all lines of a settlement
/// at once rather than merging them individually.
/// </remarks>
public sealed class SettlementLine
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>Owning settlement.</summary>
    public required string SettlementId { get; set; }

    /// <summary>When the transaction posted.</summary>
    public DateTimeOffset? PostedDate { get; set; }

    /// <summary>Transaction type, e.g. Order, Refund, ServiceFee.</summary>
    public required string TransactionType { get; set; }

    /// <summary>Related Amazon order, if any.</summary>
    public string? AmazonOrderId { get; set; }

    /// <summary>Related SKU, if any.</summary>
    public string? Sku { get; set; }

    /// <summary>Amount type, e.g. ItemPrice, ItemFees, Promotion.</summary>
    public string? AmountType { get; set; }

    /// <summary>Amount description, e.g. Principal, Commission.</summary>
    public string? AmountDescription { get; set; }

    /// <summary>Signed amount; fees and refunds are negative.</summary>
    public decimal Amount { get; set; }

    /// <summary>Navigation to the owning settlement.</summary>
    public Settlement? Settlement { get; set; }
}
