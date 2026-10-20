namespace AERai.Web.Application.Ingestion;

/// <summary>One product on a deferred transaction, from its item's <c>ProductContext</c> and amounts.</summary>
/// <param name="Sku">Seller SKU, when given.</param>
/// <param name="Asin">ASIN, when given.</param>
/// <param name="Quantity">Units shipped.</param>
/// <param name="Principal">Product charges (price times quantity), for the price each.</param>
/// <param name="Amount">The item's total after Amazon's fees (signed).</param>
public sealed record AmazonDeferredProduct(string? Sku, string? Asin, int Quantity, decimal Principal, decimal Amount);
