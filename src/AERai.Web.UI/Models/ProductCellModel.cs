namespace AERai.Web.UI.Models;

/// <summary>What the shared product cell (<c>_ProductCell</c>) shows: picture, title, SKU · ASIN, family.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Asin">ASIN, when known.</param>
/// <param name="Title">Product title, when known (the SKU is shown instead).</param>
/// <param name="Family">Family name, when assigned.</param>
/// <param name="ImagePath">Blob path of the picture, when set; its file name versions the image URL so a new picture isn't cached as the old one.</param>
/// <param name="EditUrl">Where the title links (an edit page), or <see langword="null"/> for plain text.</param>
public sealed record ProductCellModel(string Sku, string? Asin, string? Title, string? Family, string? ImagePath, string? EditUrl);
