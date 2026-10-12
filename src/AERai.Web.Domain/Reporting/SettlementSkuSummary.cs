namespace AERai.Web.Domain.Reporting;

/// <summary>
/// Read model for <c>rpt.vw_SettlementSkuSummary</c>: one row per settlement, SKU, sale price, and
/// currency, with the same Sales/Fees/Refunds/Other columns as <see cref="SettlementSummary"/>.
/// </summary>
public sealed class SettlementSkuSummary
{
    /// <summary>Marketplace the payout settles.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Amazon settlement identifier.</summary>
    public required string SettlementId { get; set; }

    /// <summary>Start of the settlement period.</summary>
    public DateTimeOffset? PeriodStart { get; set; }

    /// <summary>End of the settlement period.</summary>
    public DateTimeOffset? PeriodEnd { get; set; }

    /// <summary>ISO 4217 currency code.</summary>
    public string? Currency { get; set; }

    /// <summary>Seller SKU; <see langword="null"/> for amounts that belong to no SKU (service fees).</summary>
    public string? Sku { get; set; }

    /// <summary>ASIN from the product catalog, when the SKU is known there.</summary>
    public string? Asin { get; set; }

    /// <summary>Product title from the catalog, when known.</summary>
    public string? Title { get; set; }

    /// <summary>Price per item: the Principal divided by the units on the order item (the whole Principal when the units are unknown); <see langword="null"/> when the row has none.</summary>
    public decimal? SalePrice { get; set; }

    /// <summary>Units sold at this price (quantity on the order price lines; refunds do not reduce it).</summary>
    public int Units { get; set; }

    /// <summary>Distinct orders in the group.</summary>
    public int OrderCount { get; set; }

    /// <summary>Settlement lines in the group.</summary>
    public int LineCount { get; set; }

    /// <summary>Item price amounts on order transactions.</summary>
    public decimal Sales { get; set; }

    /// <summary>Selling and fulfillment fees (negative).</summary>
    public decimal Fees { get; set; }

    /// <summary>Net of refund transactions (usually negative).</summary>
    public decimal Refunds { get; set; }

    /// <summary>Everything else: promotions, reimbursements, adjustments.</summary>
    public decimal Other { get; set; }

    /// <summary>Sum of all amounts in the group.</summary>
    public decimal Net { get; set; }
}
