namespace AERai.Web.Domain.Core;

/// <summary>
/// Curated master record for a sellable SKU, shared by every marketplace. Identity fields are
/// maintained by promotion; costs are per marketplace in <see cref="ProductCost"/>.
/// </summary>
public sealed class Product
{
    /// <summary>Seller SKU (natural key).</summary>
    public required string Sku { get; set; }

    /// <summary>Amazon Standard Identification Number, when known.</summary>
    public string? Asin { get; set; }

    /// <summary>Most recent product title seen in an import.</summary>
    public string? Title { get; set; }

    /// <summary>When the SKU was first seen.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When any field was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
