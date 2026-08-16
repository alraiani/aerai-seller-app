using System.Globalization;
using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Staging;
using AERai.Seller.SpApiClient.Orders;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Application.Sync;

public interface IOrderSyncService
{
    /// <summary>Pulls orders placed on <paramref name="date"/> only. Orders already present locally are
    /// left untouched (matched by AmazonOrderId) — only new orders get inserted.</summary>
    Task SyncAsync(DateOnly date, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls all orders from <paramref name="days"/> ago through now in a single ranged/paginated
    /// query — not a day-by-day loop, since Orders.SearchOrders's rate limit (0.0056 rps, burst 20)
    /// would make 90 sequential day-scoped calls take hours. Each page is upserted immediately, so
    /// cancelling partway through keeps whatever pages already completed.
    /// </summary>
    Task<OrderBackfillResult> BackfillLastNDaysAsync(
        int days, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

public sealed record OrderBackfillResult(int PagesFetched, int TotalSeen, int TotalNew);

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
            // "date" is a Pacific-Time calendar day (Amazon Seller Central's "today" convention —
            // see AmazonBusinessDay), not a UTC one, so the fetch window has to be computed the
            // same way or the two disagree at the day boundary.
            var createdAfter = AmazonBusinessDay.StartOfDayUtc(date);
            var startOfNextDay = AmazonBusinessDay.StartOfDayUtc(date.AddDays(1));
            var createdBefore = ComputeCreatedBefore(createdAfter, startOfNextDay, timeProvider.GetUtcNow());

            var (_, totalSeen, totalNew) = await FetchAndUpsertOrdersAsync(
                createdAfter, createdBefore, $"Orders ({date:yyyy-MM-dd})", progress, cancellationToken);

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

    public async Task<OrderBackfillResult> BackfillLastNDaysAsync(
        int days, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var now = timeProvider.GetUtcNow();
            var createdAfter = AmazonBusinessDay.StartOfDayUtc(AmazonBusinessDay.TodayIn(now).AddDays(-days));
            var createdBefore = now - CreatedBeforeSafetyBuffer;

            var (pagesFetched, totalSeen, totalNew) = await FetchAndUpsertOrdersAsync(
                createdAfter, createdBefore, $"Orders (last {days} days)", progress, cancellationToken);

            await syncMetadataRepository.RecordResultAsync(SyncJobName, succeeded: true, errorMessage: null, cancellationToken);
            progress?.Report(
                $"Orders: backfill done — {totalNew} new, {totalSeen - totalNew} already synced, across {pagesFetched} page(s).");
            logger.LogInformation(
                "Order backfill completed for last {Days} days: {New} new / {Total} seen across {Pages} page(s)",
                days, totalNew, totalSeen, pagesFetched);

            return new OrderBackfillResult(pagesFetched, totalSeen, totalNew);
        }
        catch (OperationCanceledException)
        {
            // Every page fetched before cancellation was already upserted, so that partial
            // progress is intentionally kept — this is a user-cancelled partial pull, not a
            // failure, so it isn't recorded as one (RecordResultAsync would need the same
            // already-cancelled token anyway).
            logger.LogInformation("Order backfill for last {Days} days was cancelled; partial progress retained", days);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Order backfill failed");
            await syncMetadataRepository.RecordResultAsync(SyncJobName, succeeded: false, ex.Message, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Shared paging loop: pages through searchOrders for a single createdAfter/createdBefore
    /// window, upserting each page immediately (not batched at the end) so partial results
    /// survive a cancellation or later failure. Used by both the day-scoped sync and the
    /// range-scoped backfill.
    /// </summary>
    private async Task<(int PagesFetched, int TotalSeen, int TotalNew)> FetchAndUpsertOrdersAsync(
        DateTimeOffset createdAfter,
        DateTimeOffset? createdBefore,
        string progressLabel,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var totalSeen = 0;
        var totalNew = 0;
        var pageNumber = 0;
        string? paginationToken = null;

        do
        {
            pageNumber++;
            progress?.Report($"{progressLabel}: fetching page {pageNumber}...");

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

            progress?.Report($"{progressLabel}: checking {orders.Count} orders from page {pageNumber} against local data...");
            var inserted = await orderRepository.InsertNewOrdersAsync(orders, cancellationToken);
            totalNew += inserted;

            paginationToken = page.Pagination?.NextToken;
        }
        while (paginationToken is not null);

        return (pageNumber, totalSeen, totalNew);
    }

    /// <summary>
    /// Normally <paramref name="startOfNextDay"/> (i.e. an exact single-day window). For today (or
    /// a date whose "next day" boundary is within the safety buffer of now), that upper bound would
    /// be in the future, which SP-API rejects — clamp to just-before-now instead, or omit
    /// createdBefore entirely if even that clamped value wouldn't leave a valid window yet
    /// (e.g. syncing "today" in the first few minutes after Pacific midnight).
    /// </summary>
    private static DateTimeOffset? ComputeCreatedBefore(DateTimeOffset createdAfter, DateTimeOffset startOfNextDay, DateTimeOffset now)
    {
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
