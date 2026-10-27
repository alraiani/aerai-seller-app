namespace AERai.Web.Application.Inventory;

/// <summary>Units of a SKU sold in one calendar month (UTC; cancelled orders excluded).</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Year">Year.</param>
/// <param name="Month">Month, 1–12.</param>
/// <param name="Units">Units sold.</param>
public sealed record MonthlyUnitsSold(string Sku, int Year, int Month, int Units);
