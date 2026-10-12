namespace AERai.Web.Domain.Core;

/// <summary>
/// A curated customer order, de-duplicated by <see cref="AmazonOrderId"/>.
/// </summary>
public sealed class Order
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>Amazon order identifier (natural key).</summary>
    public required string AmazonOrderId { get; set; }

    /// <summary>Marketplace the order was placed in.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>When the customer placed the order.</summary>
    public DateTimeOffset PurchaseDate { get; set; }

    /// <summary>
    /// UTC calendar date of <see cref="PurchaseDate"/>. Computed and persisted by the database so
    /// daily reporting can filter and group on an indexed column.
    /// </summary>
    public DateOnly PurchaseDateUtc { get; private set; }

    /// <summary>Latest known order status, e.g. Shipped, Pending, Cancelled.</summary>
    public required string OrderStatus { get; set; }

    /// <summary>ISO 4217 currency code of the order's amounts.</summary>
    public string? Currency { get; set; }

    /// <summary>The staging batch that last updated this order (lineage).</summary>
    public long LastImportBatchId { get; set; }

    /// <summary>When promotion last updated this order.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Line items on the order.</summary>
    public List<OrderItem> Items { get; set; } = [];
}
