using System.Globalization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Orders;

/// <summary>
/// Default <see cref="IOrderService"/>: reads one page of orders, then their items and settlement amounts
/// in two queries, and combines them into payouts and profit estimates.
/// </summary>
/// <param name="queries">Order queries.</param>
/// <param name="time">Clock (for the export's file name).</param>
public sealed class OrderService(IOrderQueries queries, TimeProvider time) : IOrderService
{
    /// <summary>The CSV holds at most this many rows, keeping the download and its query bounded.</summary>
    public const int MaxExportRows = 50_000;

    /// <inheritdoc/>
    public async Task<OrderList> GetListAsync(Marketplace marketplace, PageRequest request, OrderListFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filter);

        var page = await queries.GetOrdersAsync(marketplace.MarketplaceId, request, filter, cancellationToken).ConfigureAwait(false);
        var totals = await queries.GetTotalsAsync(marketplace.MarketplaceId, request, filter, cancellationToken).ConfigureAwait(false);
        var statuses = await queries.GetStatusesAsync(marketplace.MarketplaceId, cancellationToken).ConfigureAwait(false);

        var ids = page.Items.Select(o => o.AmazonOrderId).ToList();
        if (ids.Count == 0)
        {
            return new OrderList(new PagedResult<OrderListItem>([], page.TotalCount, page.Page, page.PageSize), totals, statuses);
        }

        var items = (await queries.GetItemsAsync(marketplace.MarketplaceId, ids, cancellationToken).ConfigureAwait(false))
            .ToLookup(i => i.AmazonOrderId, StringComparer.Ordinal);
        var settled = (await queries.GetSettlementAmountsAsync(marketplace.SettlementMarketplaceId ?? marketplace.MarketplaceId, ids, cancellationToken).ConfigureAwait(false))
            .ToLookup(a => a.AmazonOrderId, StringComparer.Ordinal);

        var rows = page.Items.Select(order =>
        {
            var orderItems = items[order.AmazonOrderId].ToList();
            // The settlements list can hold payouts in several currencies (the US list holds Canada's in
            // CAD), so only amounts in the order's own currency are its payout: adding the rest would mix
            // currencies.
            var amounts = settled[order.AmazonOrderId]
                .Where(a => order.Currency is null || a.Currency is null || string.Equals(a.Currency, order.Currency, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var payout = OrderPayout.From(amounts);
            return new OrderListItem(order, orderItems, payout, OrderProfit.Estimate(order.OrderStatus, orderItems, payout));
        }).ToList();

        return new OrderList(new PagedResult<OrderListItem>(rows, page.TotalCount, page.Page, page.PageSize), totals, statuses);
    }

    /// <inheritdoc/>
    public async Task<OrderExport> ExportAsync(Marketplace marketplace, PageRequest request, OrderListFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filter);

        // One more than the cap tells a full export from one that was cut short.
        var rows = await queries.GetExportRowsAsync(marketplace.MarketplaceId, request, filter, MaxExportRows + 1, cancellationToken).ConfigureAwait(false);
        var truncated = rows.Count > MaxExportRows;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(marketplace.TimeZoneId);
        var today = LocalTime.DateOf(time.GetUtcNow(), zone);
        // A cut-short file says so in its name, since a download has nowhere else to show a message.
        var fileName = string.Create(CultureInfo.InvariantCulture, $"orders-{marketplace.Code}-{today:yyyy-MM-dd}{(truncated ? $"-first-{MaxExportRows}-rows" : "")}.csv");
        return new OrderExport(fileName, OrderCsvWriter.Write(truncated ? rows.Take(MaxExportRows) : rows, zone), truncated);
    }
}
