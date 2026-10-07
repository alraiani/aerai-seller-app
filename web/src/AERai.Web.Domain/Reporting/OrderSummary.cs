namespace AERai.Web.Domain.Reporting;

/// <summary>
/// Read model for <c>rpt.vw_OrderSummary</c>: one row per order with item totals.
/// </summary>
public sealed class OrderSummary
{
    /// <summary>Amazon order identifier.</summary>
    public required string AmazonOrderId { get; set; }

    /// <summary>Marketplace the order was placed in.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>When the order was placed.</summary>
    public DateTimeOffset PurchaseDate { get; set; }

    /// <summary>Latest order status.</summary>
    public required string OrderStatus { get; set; }

    /// <summary>ISO 4217 currency code.</summary>
    public string? Currency { get; set; }

    /// <summary>Number of distinct SKUs on the order.</summary>
    public int LineCount { get; set; }

    /// <summary>Total units on the order.</summary>
    public int Units { get; set; }

    /// <summary>Sum of item prices.</summary>
    public decimal OrderTotal { get; set; }
}
