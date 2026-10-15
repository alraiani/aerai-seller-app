namespace AERai.Web.Application.Products;

/// <summary>One SKU whose cost of goods is to change, with the cost it had when the change was made.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Current">Cost when the page or sheet was read (<see langword="null"/> = not set). Saving skips the SKU if it no longer matches.</param>
/// <param name="New">New cost, or <see langword="null"/> to clear it.</param>
/// <param name="Title">Product title, for display.</param>
public sealed record ProductCostChange(string Sku, decimal? Current, decimal? New, string? Title = null);
