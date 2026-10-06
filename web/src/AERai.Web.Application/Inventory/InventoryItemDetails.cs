namespace AERai.Web.Application.Inventory;

/// <summary>The user-editable details of one SKU in one marketplace.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Asin">ASIN, when known.</param>
/// <param name="Title">Product title, when known.</param>
/// <param name="Family">Family name, if assigned.</param>
/// <param name="ImagePath">Blob path of the picture, if one was uploaded.</param>
/// <param name="HomeStock">Units held outside Amazon for the marketplace.</param>
public sealed record InventoryItemDetails(string Sku, string? Asin, string? Title, string? Family, string? ImagePath, int HomeStock);
