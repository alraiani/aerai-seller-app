namespace AERai.Web.Domain.Core;

/// <summary>
/// Units of a SKU the seller holds outside Amazon (at home or in their own warehouse) for one
/// marketplace, entered by hand or uploaded from a spreadsheet. No row means none.
/// </summary>
/// <remarks>
/// Per marketplace because stock is never pooled: units set aside for Canada cannot be sent to a
/// US fulfillment center as part of the US plan, and vice versa.
/// </remarks>
public sealed class HomeStock
{
    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>Marketplace the stock is held for.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Units on hand.</summary>
    public int Quantity { get; set; }

    /// <summary>When the quantity was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Email of the user who last changed it.</summary>
    public required string UpdatedBy { get; set; }
}
