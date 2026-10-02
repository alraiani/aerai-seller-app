namespace AERai.Web.Application.Products;

/// <summary>
/// A product as listed on the Products page.
/// </summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Asin">ASIN, when known.</param>
/// <param name="Title">Product title, when known.</param>
/// <param name="CostOfGoods">Unit cost, when entered.</param>
/// <param name="UpdatedAt">Last change time.</param>
public sealed record ProductSummary(string Sku, string? Asin, string? Title, decimal? CostOfGoods, DateTimeOffset UpdatedAt);
