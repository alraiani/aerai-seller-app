using System.Net.Http.Json;
using System.Text.Json;

namespace AERai.Seller.SpApiClient.Orders;

/// <summary>Typed client for the SP-API Orders model (v0). All calls route through <see cref="SpApiRequestPipeline"/>.</summary>
public sealed class OrdersApiClient(SpApiRequestPipeline pipeline, ICredentialStore credentialStore)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GetOrdersResult> GetOrdersAsync(
        DateTimeOffset createdAfter,
        string? nextToken,
        CancellationToken cancellationToken)
    {
        var credentials = await credentialStore.GetCredentialsAsync(cancellationToken)
            ?? throw new InvalidOperationException("No SP-API credentials configured. Set them in Settings first.");

        using var response = await pipeline.SendAsync(
            "Orders.GetOrders",
            host =>
            {
                var query = nextToken is not null
                    ? $"MarketplaceIds={Uri.EscapeDataString(credentials.MarketplaceId)}&NextToken={Uri.EscapeDataString(nextToken)}"
                    : $"MarketplaceIds={Uri.EscapeDataString(credentials.MarketplaceId)}&CreatedAfter={Uri.EscapeDataString(createdAfter.ToString("O"))}";

                return new HttpRequestMessage(HttpMethod.Get, $"{host}/orders/v0/orders?{query}");
            },
            cancellationToken);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<GetOrdersResult>>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("GetOrders response was empty.");
        return envelope.Payload;
    }

    public async Task<IReadOnlyList<AmazonOrderItem>> GetOrderItemsAsync(string amazonOrderId, CancellationToken cancellationToken)
    {
        using var response = await pipeline.SendAsync(
            "Orders.GetOrderItems",
            host => new HttpRequestMessage(HttpMethod.Get, $"{host}/orders/v0/orders/{amazonOrderId}/orderItems"),
            cancellationToken);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<GetOrderItemsResult>>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("GetOrderItems response was empty.");
        return envelope.Payload.OrderItems;
    }

    private sealed record Envelope<T>(T Payload);

    public sealed record GetOrdersResult(IReadOnlyList<AmazonOrder> Orders, string? NextToken);

    private sealed record GetOrderItemsResult(IReadOnlyList<AmazonOrderItem> OrderItems, string? NextToken);

    public sealed record AmazonOrder(
        string AmazonOrderId,
        string MarketplaceId,
        DateTimeOffset PurchaseDate,
        DateTimeOffset LastUpdateDate,
        string OrderStatus,
        OrderTotal? OrderTotal);

    public sealed record OrderTotal(string CurrencyCode, decimal Amount);

    public sealed record AmazonOrderItem(
        string? SellerSku,
        string? Asin,
        string? Title,
        int QuantityOrdered,
        OrderTotal? ItemPrice);
}
