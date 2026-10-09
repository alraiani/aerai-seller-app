namespace AERai.Web.Application.Inventory;

/// <summary>A SKU as seen by picture imports: what a file or Amazon listing can be matched by.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Asin">ASIN, when known.</param>
/// <param name="HasImage">Whether the SKU already has a picture.</param>
public sealed record PictureTarget(string Sku, string? Asin, bool HasImage);
