namespace AERai.Web.Application.Advertising;

/// <summary>One advertised SKU on the Advertising page.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Asin">ASIN, from the catalog or the ads.</param>
/// <param name="Title">Catalog title.</param>
/// <param name="ImagePath">Catalog picture, if any.</param>
/// <param name="FamilyId">Product family, if assigned.</param>
/// <param name="Family">Family name, if assigned.</param>
/// <param name="Campaigns">Campaigns the SKU was advertised in over the days shown.</param>
/// <param name="Metrics">Its figures over the days shown.</param>
public sealed record AdSkuRow(
    string Sku, string? Asin, string? Title, string? ImagePath, int? FamilyId, string? Family, int Campaigns, AdMetrics Metrics);
