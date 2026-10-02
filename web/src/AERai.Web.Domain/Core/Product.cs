namespace AERai.Web.Domain.Core;

/// <summary>
/// Curated master record for a sellable SKU. Identity fields are maintained by promotion;
/// <see cref="CostOfGoods"/> is entered by users and is never overwritten by imports.
/// </summary>
public sealed class Product
{
    /// <summary>Seller SKU (natural key).</summary>
    public required string Sku { get; set; }

    /// <summary>Amazon Standard Identification Number, when known.</summary>
    public string? Asin { get; set; }

    /// <summary>Most recent product title seen in an import.</summary>
    public string? Title { get; set; }

    /// <summary>Landed cost per unit, entered manually; <see langword="null"/> until set.</summary>
    public decimal? CostOfGoods { get; set; }

    /// <summary>When the SKU was first seen.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When any field was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
