namespace AERai.Web.Application.Products;

/// <summary>
/// A product as listed on the Products page.
/// </summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Asin">ASIN, when known.</param>
/// <param name="Title">Product title, when known.</param>
/// <param name="CostOfGoods">Unit cost in the listed marketplace's currency, when entered.</param>
/// <param name="UpdatedAt">Last change to the product or its cost.</param>
/// <param name="FamilyId">The product's family, if assigned.</param>
/// <param name="Family">The family's name, if assigned.</param>
/// <param name="ImagePath">Blob path of the product picture, if one was set (its name changes with each new picture).</param>
/// <param name="CostUpdatedAt">When the cost was last set in this marketplace, or <see langword="null"/> when it is not set.</param>
public sealed record ProductSummary(
    string Sku,
    string? Asin,
    string? Title,
    decimal? CostOfGoods,
    DateTimeOffset UpdatedAt,
    int? FamilyId = null,
    string? Family = null,
    string? ImagePath = null,
    DateTimeOffset? CostUpdatedAt = null);
