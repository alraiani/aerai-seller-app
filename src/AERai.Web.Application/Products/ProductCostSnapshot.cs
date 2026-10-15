namespace AERai.Web.Application.Products;

/// <summary>A product's title and current cost in one marketplace, for checking an upload.</summary>
/// <param name="Title">Product title, when known.</param>
/// <param name="CostOfGoods">Current cost, or <see langword="null"/> when not set.</param>
public sealed record ProductCostSnapshot(string? Title, decimal? CostOfGoods);
