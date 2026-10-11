namespace AERai.Web.Application.Ingestion;

/// <summary>An order as the Orders API returns it, with its line items inline.</summary>
/// <param name="AmazonOrderId">Amazon order identifier.</param>
/// <param name="PurchaseDate">When the customer placed the order.</param>
/// <param name="LastUpdatedAt">When Amazon last changed the order.</param>
/// <param name="Status">Order status in the spelling reports use (<c>Shipped</c>, <c>Pending</c>, <c>Cancelled</c>, …).</param>
/// <param name="Currency">ISO 4217 code of the order's amounts, when Amazon sent one.</param>
/// <param name="Lines">The SKU lines; a SKU appears once.</param>
public sealed record AmazonOrder(
    string AmazonOrderId,
    DateTimeOffset PurchaseDate,
    DateTimeOffset LastUpdatedAt,
    string Status,
    string? Currency,
    IReadOnlyList<AmazonOrderLine> Lines);
