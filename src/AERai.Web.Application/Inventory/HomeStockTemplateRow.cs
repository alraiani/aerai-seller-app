using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>
/// One SKU in the downloadable home-stock template. Only <see cref="Sku"/> and
/// <see cref="HomeStock"/> are read back on upload; the rest is there to help whoever fills it in.
/// </summary>
/// <param name="Sku">Seller SKU (the <c>sku</c> column).</param>
/// <param name="HomeStock">Current home stock (the <c>home-stock</c> column, to overwrite with the count).</param>
/// <param name="Title">Product title.</param>
/// <param name="Family">Family name, if assigned.</param>
/// <param name="Color">Color, if set.</param>
/// <param name="AtAmazon">Units sellable at Amazon.</param>
/// <param name="Inbound">Units on their way into Amazon.</param>
/// <param name="Sold30d">Units sold in the last 30 days.</param>
/// <param name="SendToAmazon">Units the restock plan says to send from home, if any.</param>
public sealed record HomeStockTemplateRow(
    string Sku,
    int HomeStock,
    string? Title,
    string? Family,
    ProductColor? Color,
    int AtAmazon,
    int Inbound,
    int Sold30d,
    int? SendToAmazon);
