namespace AERai.Web.Domain.Reporting;

/// <summary>
/// Read model for <c>rpt.vw_SettlementLine</c>: one settlement line with the bucket column it counts
/// toward, so the plusses and minuses explain a <see cref="SettlementSkuSummary"/> row.
/// </summary>
public sealed class SettlementLineDetail
{
    /// <summary>Marketplace the payout settles.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Amazon settlement identifier.</summary>
    public required string SettlementId { get; set; }

    /// <summary>ISO 4217 currency code.</summary>
    public string? Currency { get; set; }

    /// <summary>Id of the underlying settlement line.</summary>
    public long LineId { get; set; }

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

    /// <summary>Principal price of the line's order item; matches <see cref="SettlementSkuSummary.SalePrice"/>.</summary>
    public decimal? SalePrice { get; set; }

    /// <summary>Signed amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>The amount when it counts as a sale, otherwise zero.</summary>
    public decimal Sales { get; set; }

    /// <summary>The amount when it counts as a fee, otherwise zero.</summary>
    public decimal Fees { get; set; }

    /// <summary>The amount when it counts as a refund, otherwise zero.</summary>
    public decimal Refunds { get; set; }

    /// <summary>The amount when it counts as other, otherwise zero.</summary>
    public decimal Other { get; set; }
}
