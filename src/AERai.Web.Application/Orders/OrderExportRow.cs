namespace AERai.Web.Application.Orders;

/// <summary>One item of one order, as a row of the Orders CSV.</summary>
/// <param name="AmazonOrderId">Amazon order identifier.</param>
/// <param name="PurchaseDate">When the order was placed.</param>
/// <param name="OrderStatus">Latest order status.</param>
/// <param name="Currency">ISO 4217 currency of the amounts.</param>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Asin">ASIN from the catalog, if known.</param>
/// <param name="Title">Title from the catalog, if known.</param>
/// <param name="Quantity">Units ordered.</param>
/// <param name="ItemPrice">Line total charged (quantity × unit price).</param>
/// <param name="UnitCost">Cost of goods per unit in the marketplace, or <see langword="null"/> when not set.</param>
public sealed record OrderExportRow(
    string AmazonOrderId,
    DateTimeOffset PurchaseDate,
    string OrderStatus,
    string? Currency,
    string Sku,
    string? Asin,
    string? Title,
    int Quantity,
    decimal ItemPrice,
    decimal? UnitCost);
