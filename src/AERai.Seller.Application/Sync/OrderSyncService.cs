using System.Globalization;
using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using AERai.Seller.SpApiClient.Orders;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Application.Sync;

public interface IOrderSyncService
{
    /// <summary>Pulls orders placed on <paramref name="date"/> only. Orders already present locally are
    /// left untouched (matched by AmazonOrderId) — only new orders get inserted.</summary>
    Task SyncAsync(DateOnly date, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pulls a single day's orders via searchOrders (2026-01-01), which returns each order's line
/// items inline — no separate per-order follow-up call needed. Scoped to one day at a time
/// (rather than an open-ended incremental window) so a manual sync stays fast and predictable.
/// </summary>
public sealed class OrderSyncService(
    OrdersApiClient ordersApiClient,
    IOrderRepository orderRepository,
    ISyncMetadataRepository syncMetadataRepository,
    TimeProvider timeProvider,
    ILogger<OrderSyncService> logger) : IOrderSyncService
{
    public const string SyncJobName = "Orders";

    // SP-API requires createdBefore to be at least 2 minutes before "now" — use a slightly
    // larger buffer for clock-skew safety.
    private static readonly TimeSpan CreatedBeforeSafetyBuffer = TimeSpan.FromMinutes(3);

    public async Task SyncAsync(DateOnly date, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var createdAfter = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            var createdBefore = ComputeCreatedBefore(createdAfter, timeProvider.GetUtcNow());

            var totalSeen = 0;
            var totalNew = 0;
            var pageNumber = 0;
            string? paginationToken = null;

            do
            {
                pageNumber++;
                progress?.Report($"Orders: fetching {date:yyyy-MM-dd} page {pageNumber}...");

                // createdAfter/createdBefore must be resent on every page, not just the first —
                // see the comment in OrdersApiClient.SearchOrdersAsync for why.
                var page = await ordersApiClient.SearchOrdersAsync(
                    createdAfter: createdAfter,
                    createdBefore: createdBefore,
                    lastUpdatedAfter: null,
                    paginationToken,
                    cancellationToken);

                var orders = page.Orders.Select(MapOrder).ToList();
                totalSeen += orders.Count;

                progress?.Report($"Orders: checking {orders.Count} orders from page {pageNumber} against local data...");
                var inserted = await orderRepository.InsertNewOrdersAsync(orders, cancellationToken);
                totalNew += inserted;

                paginationToken = page.Pagination?.NextToken;
            }
            while (paginationToken is not null);

            await syncMetadataRepository.RecordResultAsync(SyncJobName, succeeded: true, errorMessage: null, cancellationToken);
            progress?.Report($"Orders: done — {totalNew} new, {totalSeen - totalNew} already synced.");
            logger.LogInformation(
                "Order sync completed for {Date}: {New} new / {Total} seen", date, totalNew, totalSeen);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Order sync failed");
            await syncMetadataRepository.RecordResultAsync(SyncJobName, succeeded: false, ex.Message, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Normally the day after <paramref name="createdAfter"/> (i.e. an exact single-day window).
    /// For today (or a date whose "next day" boundary is within the safety buffer of now), that
    /// upper bound would be in the future, which SP-API rejects — clamp to just-before-now instead,
    /// or omit createdBefore entirely if even that clamped value wouldn't leave a valid window yet
    /// (e.g. syncing "today" in the first few minutes after midnight).
    /// </summary>
    private static DateTimeOffset? ComputeCreatedBefore(DateTimeOffset createdAfter, DateTimeOffset now)
    {
        var startOfNextDay = createdAfter.AddDays(1);
        var latestAllowed = now - CreatedBeforeSafetyBuffer;

        if (startOfNextDay <= latestAllowed)
        {
            return startOfNextDay;
        }

        return latestAllowed > createdAfter ? latestAllowed : null;
    }

    private static Order MapOrder(OrdersApiClient.AmazonOrder amazonOrder)
    {
        var order = new Order
        {
            AmazonOrderId = amazonOrder.OrderId,
            MarketplaceId = amazonOrder.SalesChannel?.MarketplaceId ?? "UNKNOWN",
            PurchaseDate = amazonOrder.CreatedTime,
            OrderStatus = amazonOrder.Fulfillment?.FulfillmentStatus ?? "UNKNOWN",
            OrderTotalAmount = ParseAmount(amazonOrder.Proceeds?.GrandTotal?.Amount),
            OrderTotalCurrency = amazonOrder.Proceeds?.GrandTotal?.CurrencyCode,
            LastUpdatedAt = amazonOrder.LastUpdatedTime,
        };

        order.Items = amazonOrder.OrderItems
            .Where(i => i.Product.SellerSku is not null)
            .Select(i => new OrderItem
            {
                AmazonOrderId = amazonOrder.OrderId,
                Sku = i.Product.SellerSku!,
                Asin = i.Product.Asin,
                Title = i.Product.Title,
                QuantityOrdered = i.QuantityOrdered,
                ItemPrice = ParseAmount(i.Product.Price?.UnitPrice?.Amount),
                ItemPriceCurrency = i.Product.Price?.UnitPrice?.CurrencyCode,
            })
            .ToList();

        return order;
    }

    private static decimal? ParseAmount(string? amount)
        => amount is not null && decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
