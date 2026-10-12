namespace AERai.Web.Application.Inventory;

/// <summary>A home-stock count for one SKU.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Quantity">Units on hand (0 clears it).</param>
public sealed record HomeStockEntry(string Sku, int Quantity);
