namespace AERai.Web.Domain.Reporting;

/// <summary>
/// Read model for <c>rpt.vw_SalesLine</c>: one sold order item (cancelled orders excluded), with the
/// exact purchase timestamp so the dashboard can bucket sales by the business's local day and hour.
/// </summary>
public sealed class SalesLine
{
    /// <summary>Amazon order identifier.</summary>
    public required string AmazonOrderId { get; set; }

    /// <summary>Marketplace the order was placed in.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>When the order was placed.</summary>
    public DateTimeOffset PurchaseDate { get; set; }

    /// <summary>Order status, e.g. Shipped or Pending.</summary>
    public required string OrderStatus { get; set; }

    /// <summary>ISO 4217 currency code; <see langword="null"/> when Amazon left it blank.</summary>
    public string? Currency { get; set; }

    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>Product title, when known.</summary>
    public string? Title { get; set; }

    /// <summary>Units on this line.</summary>
    public int Quantity { get; set; }

    /// <summary>Line total (quantity × unit price).</summary>
    public decimal ItemPrice { get; set; }
}
