using System.Globalization;
using System.Text.Json;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Reads an Orders API <c>searchOrders</c> response body into orders. Shared by the live client and the
/// blob refresh, so a page parses the same whether it just arrived from Amazon or was uploaded later.
/// </summary>
public static class OrdersPageParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Parses a <c>searchOrders</c> response body.</summary>
    /// <param name="json">The response body.</param>
    /// <param name="fallbackCurrency">Currency used when Amazon sends none for an order.</param>
    /// <returns>The page. Items without a SKU are dropped, and a SKU repeated within an order is summed.</returns>
    /// <exception cref="InvalidOperationException">The body is empty.</exception>
    /// <exception cref="JsonException">The body is not valid JSON.</exception>
    public static AmazonOrdersPage Parse(string json, string? fallbackCurrency = null)
    {
        ArgumentNullException.ThrowIfNull(json);

        var response = JsonSerializer.Deserialize<SearchOrdersResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("The Orders API response was empty.");

        var orders = (response.Orders ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o.OrderId))
            .Select(o => new AmazonOrder(
                o.OrderId!.Trim(), // Non-null: orders without an id were filtered out just above.
                o.CreatedTime,
                o.LastUpdatedTime,
                NormalizeStatus(o.Fulfillment?.FulfillmentStatus),
                o.Proceeds?.GrandTotal?.CurrencyCode ?? fallbackCurrency,
                MapLines(o.OrderItems ?? [])))
            .ToList();
        return new AmazonOrdersPage(orders, response.Pagination?.NextToken, json);
    }

    /// <summary>
    /// Puts Amazon's upper-case status (<c>PARTIALLY_SHIPPED</c>) in the spelling the Orders report and
    /// every view already use (<c>Partially Shipped</c>), so <c>Cancelled</c> and <c>Pending</c> keep meaning
    /// the same whichever way an order arrived.
    /// </summary>
    /// <param name="fulfillmentStatus">Amazon's fulfillment status, if any.</param>
    /// <returns>Title-cased words, or <see cref="UnknownStatus"/> when Amazon sent none.</returns>
    public static string NormalizeStatus(string? fulfillmentStatus)
    {
        if (string.IsNullOrWhiteSpace(fulfillmentStatus))
        {
            return UnknownStatus;
        }

        var words = fulfillmentStatus.Trim().Split(['_', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant());
        return string.Join(' ', words);
    }

    /// <summary>The status stored when a page carries no fulfillment block (the request did not ask for it).</summary>
    public const string UnknownStatus = "Unknown";

    private static List<AmazonOrderLine> MapLines(IReadOnlyList<ApiOrderItem> items) =>
        items
            .Where(i => !string.IsNullOrWhiteSpace(i.Product?.SellerSku))
            .Select(i =>
            {
                var quantity = i.QuantityOrdered ?? 0;
                var unitPrice = decimal.TryParse(i.Product!.Price?.UnitPrice?.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
                return new AmazonOrderLine(i.Product.SellerSku!.Trim(), i.Product.Asin, i.Product.Title, quantity, decimal.Round(unitPrice * quantity, 2));
            })
            .GroupBy(l => l.Sku, StringComparer.OrdinalIgnoreCase)
            .Select(g => new AmazonOrderLine(g.First().Sku, g.First().Asin, g.First().Title, g.Sum(l => l.Quantity), g.Sum(l => l.ItemPrice)))
            .ToList();

    private sealed record SearchOrdersResponse(IReadOnlyList<ApiOrder>? Orders, ApiPagination? Pagination);

    private sealed record ApiPagination(string? NextToken);

    private sealed record ApiOrder(
        string? OrderId,
        DateTimeOffset CreatedTime,
        DateTimeOffset LastUpdatedTime,
        ApiFulfillment? Fulfillment,
        ApiProceeds? Proceeds,
        IReadOnlyList<ApiOrderItem>? OrderItems);

    private sealed record ApiFulfillment(string? FulfillmentStatus);

    private sealed record ApiProceeds(ApiMoney? GrandTotal);

    private sealed record ApiOrderItem(int? QuantityOrdered, ApiProduct? Product);

    private sealed record ApiProduct(string? Asin, string? Title, string? SellerSku, ApiPrice? Price);

    private sealed record ApiPrice(ApiMoney? UnitPrice);

    /// <summary>SP-API sends amounts as strings (<c>"49.99"</c>), not JSON numbers.</summary>
    private sealed record ApiMoney(string? Amount, string? CurrencyCode);
}
