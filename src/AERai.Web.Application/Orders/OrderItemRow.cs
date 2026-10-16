namespace AERai.Web.Application.Orders;

/// <summary>One item on an order, with what the catalog knows about its SKU.</summary>
/// <param name="AmazonOrderId">The order it belongs to.</param>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Asin">ASIN from the catalog, if known.</param>
/// <param name="Title">Title from the catalog, if known.</param>
/// <param name="Family">The SKU's family name, if it has one.</param>
/// <param name="ImagePath">The SKU's stored picture, if it has one.</param>
/// <param name="Quantity">Units ordered.</param>
/// <param name="ItemPrice">Line total charged (quantity × unit price).</param>
/// <param name="UnitCost">Cost of goods per unit in the order's marketplace, or <see langword="null"/> when not set.</param>
public sealed record OrderItemRow(
    string AmazonOrderId,
    string Sku,
    string? Asin,
    string? Title,
    string? Family,
    string? ImagePath,
    int Quantity,
    decimal ItemPrice,
    decimal? UnitCost);
