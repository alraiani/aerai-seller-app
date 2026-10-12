namespace AERai.Web.Application.Inventory;

/// <summary>Units of a SKU sold on one order (cancelled orders excluded).</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="PurchaseDate">When the order was placed.</param>
/// <param name="Quantity">Units ordered.</param>
public sealed record UnitsSold(string Sku, DateTimeOffset PurchaseDate, int Quantity);
