using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AERai.Seller.SpApiClient.Orders;

/// <summary>
/// Typed client for the SP-API Orders model (2026-01-01). All calls route through <see cref="SpApiRequestPipeline"/>.
/// This version's searchOrders returns each order with its line items inline — no separate
/// per-order call needed, unlike the older v0 getOrders + getOrderItems pair.
/// </summary>
public sealed class OrdersApiClient(SpApiRequestPipeline pipeline, ICredentialStore credentialStore)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Exactly one of <paramref name="createdAfter"/> or <paramref name="lastUpdatedAfter"/> must be supplied
    /// (SP-API requirement). <paramref name="createdBefore"/> is an optional additional upper bound, useful
    /// for scoping a single day's orders.
    /// </summary>
    public async Task<SearchOrdersResult> SearchOrdersAsync(
        DateTimeOffset? createdAfter,
        DateTimeOffset? createdBefore,
        DateTimeOffset? lastUpdatedAfter,
        string? paginationToken,
        CancellationToken cancellationToken)
    {
        var credentials = await credentialStore.GetCredentialsAsync(cancellationToken)
            ?? throw new InvalidOperationException("No SP-API credentials configured. Set them in Settings first.");

        using var response = await pipeline.SendAsync(
            "Orders.SearchOrders",
            host =>
            {
                // Despite paginationToken encoding continuation state, SP-API still requires exactly
                // one of createdAfter/lastUpdatedAfter on *every* request, including page 2+ — confirmed
                // by direct testing (page 1 succeeds, page 2 400s with "must provide exactly one" if
                // these are omitted). Always resend the original filter alongside paginationToken.
                var query = $"marketplaceIds={Uri.EscapeDataString(credentials.MarketplaceId)}";
                if (createdAfter is not null) query += $"&createdAfter={Uri.EscapeDataString(SpApiDateTimeFormatter.ToIso8601Utc(createdAfter.Value))}";
                if (lastUpdatedAfter is not null) query += $"&lastUpdatedAfter={Uri.EscapeDataString(SpApiDateTimeFormatter.ToIso8601Utc(lastUpdatedAfter.Value))}";
                if (createdBefore is not null) query += $"&createdBefore={Uri.EscapeDataString(SpApiDateTimeFormatter.ToIso8601Utc(createdBefore.Value))}";
                if (paginationToken is not null) query += $"&paginationToken={Uri.EscapeDataString(paginationToken)}";

                return new HttpRequestMessage(HttpMethod.Get, $"{host}/orders/2026-01-01/orders?{query}");
            },
            cancellationToken);

        return await response.Content.ReadFromJsonAsync<SearchOrdersResult>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("searchOrders response was empty.");
    }

    public sealed record SearchOrdersResult(IReadOnlyList<AmazonOrder> Orders, Pagination? Pagination);

    public sealed record Pagination(string? NextToken);

    public sealed record AmazonOrder(
        string OrderId,
        DateTimeOffset CreatedTime,
        DateTimeOffset LastUpdatedTime,
        SalesChannel? SalesChannel,
        Fulfillment? Fulfillment,
        Proceeds? Proceeds,
        IReadOnlyList<AmazonOrderItem> OrderItems);

    public sealed record SalesChannel(string? ChannelName, string MarketplaceId, string? MarketplaceName);

    public sealed record Fulfillment(string? FulfillmentStatus);

    public sealed record Proceeds(Money? GrandTotal);

    public sealed record AmazonOrderItem(string OrderItemId, int QuantityOrdered, Product Product);

    public sealed record Product(string? Asin, string? Title, string? SellerSku, Price? Price);

    public sealed record Price(Money? UnitPrice);

    /// <summary>SP-API returns monetary amounts as strings (e.g. "49.99"), not JSON numbers.</summary>
    public sealed record Money(
        [property: JsonPropertyName("amount")] string Amount,
        [property: JsonPropertyName("currencyCode")] string CurrencyCode);
}
