namespace AERai.Web.Application.Products;

/// <summary>One SKU on the downloadable cost sheet.</summary>
/// <param name="Sku">Seller SKU (mapped column).</param>
/// <param name="CostOfGoods">Current cost (mapped column), or <see langword="null"/> when not set.</param>
/// <param name="Asin">ASIN, for reference.</param>
/// <param name="Title">Product title, for reference.</param>
/// <param name="Family">Family name, for reference.</param>
/// <param name="CostUpdatedAt">When the cost was last set, for reference.</param>
public sealed record ProductCostTemplateRow(string Sku, decimal? CostOfGoods, string? Asin, string? Title, string? Family, DateTimeOffset? CostUpdatedAt);
