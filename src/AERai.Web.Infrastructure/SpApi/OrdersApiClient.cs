using System.Globalization;
using System.Text.Json;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Typed client for the SP-API Orders API (version 2026-01-01). Every call is tagged with its
/// operation name and region and sent through <see cref="SpApiPipelineHandler"/>.
/// </summary>
/// <remarks>
/// <c>searchOrders</c> returns each order with its line items inline, so there is no per-order
/// follow-up call as with the older v0 <c>getOrders</c> + <c>getOrderItems</c> pair.
/// </remarks>
/// <param name="http">HttpClient configured with the SP-API endpoint and pipeline handler.</param>
/// <param name="options">SP-API settings (regional endpoints).</param>
internal sealed class OrdersApiClient(HttpClient http, IOptions<SpApiOptions> options)
{
    private const string BasePath = "orders/2026-01-01";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Calls <c>searchOrders</c> for one page.</summary>
    /// <param name="marketplace">Marketplace to search; its region selects the endpoint and credentials.</param>
    /// <param name="filter">Which orders to return.</param>
    /// <param name="nextToken">Pagination token from the previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, parsed and as raw JSON.</returns>
    /// <exception cref="ArgumentException">The filter has both or neither of <c>CreatedAfter</c> and <c>LastUpdatedAfter</c>.</exception>
    public async Task<AmazonOrdersPage> SearchOrdersAsync(Marketplace marketplace, AmazonOrdersFilter filter, string? nextToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.CreatedAfter is null == filter.LastUpdatedAfter is null)
        {
            throw new ArgumentException("Exactly one of CreatedAfter and LastUpdatedAfter is required.", nameof(filter));
        }

        // Amazon wants the filter on every request, including later pages, so it is rebuilt each time.
        var query = $"marketplaceIds={Uri.EscapeDataString(marketplace.MarketplaceId)}";
        if (filter.CreatedAfter is { } createdAfter)
        {
            query += $"&createdAfter={Uri.EscapeDataString(Iso8601(createdAfter))}";
        }

        if (filter.LastUpdatedAfter is { } lastUpdatedAfter)
        {
            query += $"&lastUpdatedAfter={Uri.EscapeDataString(Iso8601(lastUpdatedAfter))}";
        }

        if (filter.CreatedBefore is { } createdBefore)
        {
            query += $"&createdBefore={Uri.EscapeDataString(Iso8601(createdBefore))}";
        }

        if (!string.IsNullOrEmpty(nextToken))
        {
            query += $"&paginationToken={Uri.EscapeDataString(nextToken)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(options.Value.EndpointFor(marketplace.Region), $"{BasePath}/orders?{query}"));
        request.Options.Set(SpApiOperation.RegionKey, marketplace.Region);
        request.Options.Set(SpApiOperation.OptionKey, SpApiOperation.SearchOrders);

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // SP-API error bodies ({"errors":[{code,message}]}) contain no secrets and help diagnose the call.
            throw new HttpRequestException(
                $"SP-API {SpApiOperation.SearchOrders} failed with HTTP {(int)response.StatusCode}: {(body.Length <= 500 ? body : body[..500])}",
                null,
                response.StatusCode);
        }

        return ParsePage(body, marketplace.Currency);
    }

    /// <summary>Parses a <c>searchOrders</c> response body.</summary>
    /// <param name="json">The response body.</param>
    /// <param name="fallbackCurrency">Currency used when Amazon sends none for an order.</param>
    /// <returns>The page. Items without a SKU are dropped, and a SKU repeated within an order is summed.</returns>
    /// <exception cref="InvalidOperationException">The body is empty or not a search result.</exception>
    public static AmazonOrdersPage ParsePage(string json, string? fallbackCurrency = null)
    {
        var response = JsonSerializer.Deserialize<SearchOrdersResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException($"SP-API {SpApiOperation.SearchOrders} returned an empty body.");

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
    /// <returns>Title-cased words, or <c>Unknown</c> when Amazon sent none.</returns>
    internal static string NormalizeStatus(string? fulfillmentStatus)
    {
        if (string.IsNullOrWhiteSpace(fulfillmentStatus))
        {
            return "Unknown";
        }

        var words = fulfillmentStatus.Trim().Split(['_', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant());
        return string.Join(' ', words);
    }

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

    // Amazon rejects fractional seconds and non-UTC offsets, so send whole-second UTC.
    private static string Iso8601(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

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
