namespace AERai.Web.Domain.Core;

/// <summary>
/// One SKU line on an <see cref="Order"/>. Unique per (order, SKU).
/// </summary>
public sealed class OrderItem
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>Owning order.</summary>
    public long OrderId { get; set; }

    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>Units ordered.</summary>
    public int Quantity { get; set; }

    /// <summary>Line total charged for the item (quantity × unit price), before fees.</summary>
    public decimal ItemPrice { get; set; }

    /// <summary>Navigation to the owning order.</summary>
    public Order? Order { get; set; }
}
