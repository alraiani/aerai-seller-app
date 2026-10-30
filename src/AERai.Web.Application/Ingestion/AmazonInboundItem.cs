namespace AERai.Web.Application.Ingestion;

/// <summary>One SKU of an Amazon inbound shipment.</summary>
/// <param name="Sku">Seller SKU (Amazon's <c>msku</c>).</param>
/// <param name="Quantity">Units of the SKU in the shipment.</param>
public sealed record AmazonInboundItem(string Sku, int Quantity);
