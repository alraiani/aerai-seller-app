namespace AERai.Web.Domain.Core;

/// <summary>
/// A SKU's landed cost per unit in one marketplace, in that marketplace's currency. Entered by users
/// and never written by imports; no row means the cost has not been set.
/// </summary>
public sealed class ProductCost
{
    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>Marketplace the cost applies to.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Cost per unit in the marketplace's currency.</summary>
    public decimal CostOfGoods { get; set; }

    /// <summary>When the cost was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
