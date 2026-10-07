namespace AERai.Web.Domain.Staging;

/// <summary>
/// A raw order-report line, as imported. Column names mirror the source report headers.
/// </summary>
public sealed class StgOrderLine : StagingRow
{
    /// <summary><c>amazon-order-id</c>.</summary>
    public string? AmazonOrderId { get; set; }

    /// <summary><c>purchase-date</c> (ISO 8601 text).</summary>
    public string? PurchaseDate { get; set; }

    /// <summary><c>order-status</c>.</summary>
    public string? OrderStatus { get; set; }

    /// <summary><c>sku</c>.</summary>
    public string? Sku { get; set; }

    /// <summary><c>asin</c>.</summary>
    public string? Asin { get; set; }

    /// <summary><c>product-name</c>.</summary>
    public string? ProductName { get; set; }

    /// <summary><c>quantity</c>.</summary>
    public string? Quantity { get; set; }

    /// <summary><c>item-price</c> (line total, not unit price).</summary>
    public string? ItemPrice { get; set; }

    /// <summary><c>currency</c>.</summary>
    public string? Currency { get; set; }

    /// <summary><c>sales-channel</c> (e.g. <c>Amazon.com</c>); optional in uploads.</summary>
    public string? SalesChannel { get; set; }
}
