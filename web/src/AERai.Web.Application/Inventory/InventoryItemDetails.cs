namespace AERai.Web.Application.Inventory;

/// <summary>The user-editable details of one SKU in one marketplace.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Asin">ASIN, when known.</param>
/// <param name="Title">Product title, when known.</param>
/// <param name="Family">Family name, if assigned.</param>
/// <param name="ImagePath">Blob path of the picture, if one was uploaded.</param>
/// <param name="HomeStock">Units held outside Amazon for the marketplace.</param>
/// <param name="LeadTimes">The SKU's own lead-time overrides in the marketplace (blank fields use the defaults).</param>
public sealed record InventoryItemDetails(string Sku, string? Asin, string? Title, string? Family, string? ImagePath, int HomeStock, LeadTimeSettings LeadTimes);
