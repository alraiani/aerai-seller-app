namespace AERai.Web.Domain.Core;

/// <summary>One SKU of an <see cref="InboundShipment"/> and the units Amazon expects of it.</summary>
public sealed class InboundShipmentItem
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>The shipment the item belongs to.</summary>
    public long InboundShipmentId { get; set; }

    /// <summary>Seller SKU (Amazon's <c>msku</c>).</summary>
    public required string Sku { get; set; }

    /// <summary>Units of the SKU in the shipment.</summary>
    public int Quantity { get; set; }
}
