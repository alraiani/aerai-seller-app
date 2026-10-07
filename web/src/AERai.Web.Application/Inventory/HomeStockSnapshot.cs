using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>A SKU's current home stock with what's needed to show it on the upload review.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="HomeStock">Units at home in the marketplace (0 when none).</param>
/// <param name="Title">Product title.</param>
/// <param name="Color">The SKU's color.</param>
public sealed record HomeStockSnapshot(string Sku, int HomeStock, string? Title, ProductColor? Color);
