namespace AERai.Web.Domain.Core;

/// <summary>
/// Curated master record for a sellable SKU, shared by every marketplace. Identity fields are
/// maintained by promotion; the family and picture are set by users on the Inventory page; costs
/// and home stock are per marketplace (<see cref="ProductCost"/>, <see cref="HomeStock"/>).
/// </summary>
public sealed class Product
{
    /// <summary>Seller SKU (natural key).</summary>
    public required string Sku { get; set; }

    /// <summary>Amazon Standard Identification Number, when known.</summary>
    public string? Asin { get; set; }

    /// <summary>Most recent product title seen in an import.</summary>
    public string? Title { get; set; }

    /// <summary>The family grouping this SKU with similar products, if assigned.</summary>
    public int? FamilyId { get; set; }

    /// <summary>Blob path of the product picture in the product-images container, if one was uploaded.</summary>
    public string? ImagePath { get; set; }

    /// <summary>MIME type of the picture (image/jpeg, image/png, or image/webp).</summary>
    public string? ImageContentType { get; set; }

    /// <summary>The variant's color, used to group and color-code SKUs on the worksheet, if set.</summary>
    public ProductColor? Color { get; set; }

    /// <summary>When the SKU was first seen.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When any field was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
