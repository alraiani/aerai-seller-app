using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using AERai.Seller.SpApiClient.Orders;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Application.Sync;

public interface IOrderSyncService
{
    Task SyncAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Pulls orders (and their line items) created since the last successful sync, and upserts them locally.
/// Primary input for sales-velocity/demand forecasting (Phase 2) and the Dashboard's today's-orders widget.
/// </summary>
public sealed class OrderSyncService(
    OrdersApiClient ordersApiClient,
    IOrderRepository orderRepository,
    ISyncMetadataRepository syncMetadataRepository,
    TimeProvider timeProvider,
    ILogger<OrderSyncService> logger) : IOrderSyncService
{
    public const string SyncJobName = "Orders";

    // First sync with no prior data pulls a rolling lookback window.
    private static readonly TimeSpan DefaultLookback = TimeSpan.FromDays(30);

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var lastPurchaseDate = await orderRepository.GetMostRecentPurchaseDateAsync(cancellationToken);
            var createdAfter = lastPurchaseDate ?? timeProvider.GetUtcNow() - DefaultLookback;

            var totalUpserted = 0;
            string? nextToken = null;

            do
            {
                var page = await ordersApiClient.GetOrdersAsync(createdAfter, nextToken, cancellationToken);
                var orders = new List<Order>(page.Orders.Count);

                foreach (var amazonOrder in page.Orders)
                {
                    var items = await ordersApiClient.GetOrderItemsAsync(amazonOrder.AmazonOrderId, cancellationToken);
                    orders.Add(MapOrder(amazonOrder, items));
                }

                await orderRepository.UpsertOrdersAsync(orders, cancellationToken);
                totalUpserted += orders.Count;
                nextToken = page.NextToken;
            }
            while (nextToken is not null);

            await syncMetadataRepository.RecordResultAsync(SyncJobName, succeeded: true, errorMessage: null, cancellationToken);
            logger.LogInformation("Order sync completed: {Count} orders upserted", totalUpserted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Order sync failed");
            await syncMetadataRepository.RecordResultAsync(SyncJobName, succeeded: false, ex.Message, cancellationToken);
            throw;
        }
    }

    private static Order MapOrder(OrdersApiClient.AmazonOrder amazonOrder, IReadOnlyList<OrdersApiClient.AmazonOrderItem> items)
    {
        var order = new Order
        {
            AmazonOrderId = amazonOrder.AmazonOrderId,
            MarketplaceId = amazonOrder.MarketplaceId,
            PurchaseDate = amazonOrder.PurchaseDate,
            OrderStatus = amazonOrder.OrderStatus,
            OrderTotalAmount = amazonOrder.OrderTotal?.Amount,
            OrderTotalCurrency = amazonOrder.OrderTotal?.CurrencyCode,
            LastUpdatedAt = amazonOrder.LastUpdateDate,
        };

        order.Items = items
            .Where(i => i.SellerSku is not null)
            .Select(i => new OrderItem
            {
                AmazonOrderId = amazonOrder.AmazonOrderId,
                Sku = i.SellerSku!,
                Asin = i.Asin,
                Title = i.Title,
                QuantityOrdered = i.QuantityOrdered,
                ItemPrice = i.ItemPrice?.Amount,
                ItemPriceCurrency = i.ItemPrice?.CurrencyCode,
            })
            .ToList();

        return order;
    }
}
