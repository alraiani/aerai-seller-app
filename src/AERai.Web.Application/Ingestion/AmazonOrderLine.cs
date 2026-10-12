namespace AERai.Web.Application.Ingestion;

/// <summary>One SKU line on an order returned by the Orders API.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Asin">Amazon ASIN, when Amazon sent one.</param>
/// <param name="Title">Product title, when Amazon sent one.</param>
/// <param name="Quantity">Units ordered.</param>
/// <param name="ItemPrice">Line total charged for the item (quantity × unit price); zero while the order is pending and unpriced.</param>
public sealed record AmazonOrderLine(string Sku, string? Asin, string? Title, int Quantity, decimal ItemPrice);
