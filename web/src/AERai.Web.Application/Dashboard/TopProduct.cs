namespace AERai.Web.Application.Dashboard;

/// <summary>A best-selling SKU in the selected period.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Title">Product title, when known.</param>
/// <param name="Units">Units sold.</param>
/// <param name="Revenue">Revenue.</param>
/// <param name="Share">Fraction of the period's total revenue (0–1).</param>
/// <param name="Change">Revenue change vs the comparison period; <see langword="null"/> if it didn't sell then.</param>
public sealed record TopProduct(string Sku, string? Title, int Units, decimal Revenue, decimal Share, decimal? Change);
