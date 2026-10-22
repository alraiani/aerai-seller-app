using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Orders;
using AERai.Web.Application.Products;
using AERai.Web.Domain.Reporting;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Queries;

/// <summary>
/// EF Core implementation of <see cref="IOrderQueries"/> over <c>rpt.vw_OrderSummary</c>,
/// <c>rpt.vw_SettlementLine</c>, and the order, product, and cost tables.
/// </summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class OrderQueries(AppDbContext dbContext) : IOrderQueries
{
    /// <inheritdoc/>
    public Task<PagedResult<OrderSummary>> GetOrdersAsync(string marketplaceId, PageRequest request, OrderListFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filter);

        return Sorted(Filter(marketplaceId, request, filter), filter).ToPagedResultAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<OrderListTotals> GetTotalsAsync(string marketplaceId, PageRequest request, OrderListFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filter);

        var cancelled = OrderStatuses.CancelledSpellings.ToList();
        var totals = await Filter(marketplaceId, request, filter)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Orders = g.Count(),
                Cancelled = g.Sum(o => cancelled.Contains(o.OrderStatus) ? 1 : 0),
                Units = g.Sum(o => cancelled.Contains(o.OrderStatus) ? 0 : o.Units),
                Sales = g.Sum(o => cancelled.Contains(o.OrderStatus) ? 0 : o.OrderTotal),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return totals is null ? OrderListTotals.Empty : new OrderListTotals(totals.Orders, totals.Cancelled, totals.Units, totals.Sales);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetStatusesAsync(string marketplaceId, CancellationToken cancellationToken) =>
        await dbContext.Orders.AsNoTracking()
            .Where(o => o.MarketplaceId == marketplaceId)
            .GroupBy(o => o.OrderStatus)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OrderItemRow>> GetItemsAsync(string marketplaceId, IReadOnlyCollection<string> amazonOrderIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(amazonOrderIds);

        var ids = amazonOrderIds.ToList();
        var rows =
            from o in dbContext.Orders.AsNoTracking()
            where o.MarketplaceId == marketplaceId && ids.Contains(o.AmazonOrderId)
            join i in dbContext.OrderItems on o.Id equals i.OrderId
            join p in dbContext.Products on i.Sku equals p.Sku into products
            from p in products.DefaultIfEmpty()
            join f in dbContext.ProductFamilies on p.FamilyId equals f.Id into families
            from f in families.DefaultIfEmpty()
            join c in dbContext.ProductCosts.Where(c => c.MarketplaceId == marketplaceId) on i.Sku equals c.Sku into costs
            from c in costs.DefaultIfEmpty()
            orderby o.AmazonOrderId, i.Sku
            select new OrderItemRow(
                o.AmazonOrderId,
                i.Sku,
                p == null ? null : p.Asin,
                p == null ? null : p.Title,
                f == null ? null : f.Name,
                p == null ? null : p.ImagePath,
                i.Quantity,
                i.ItemPrice,
                c == null ? null : c.CostOfGoods);
        return await rows.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OrderSettlementAmounts>> GetSettlementAmountsAsync(string settlementMarketplaceId, IReadOnlyCollection<string> amazonOrderIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(amazonOrderIds);

        var ids = amazonOrderIds.ToList();
        // The view holds the bucket rule (sales / fees / refunds / other), so the amounts here add up the
        // same way as the Settlements pages.
        var rows = await dbContext.SettlementLineDetails.AsNoTracking()
            .Where(l => l.MarketplaceId == settlementMarketplaceId && l.AmazonOrderId != null && ids.Contains(l.AmazonOrderId))
            .GroupBy(l => new { l.AmazonOrderId, l.SettlementId, l.Currency })
            .Select(g => new
            {
                g.Key.AmazonOrderId,
                g.Key.SettlementId,
                g.Key.Currency,
                Sales = g.Sum(l => l.Sales),
                Expenses = g.Sum(l => l.Expenses),
                Refunds = g.Sum(l => l.Refunds),
                Reserve = g.Sum(l => l.Reserve),
                FirstPosted = g.Min(l => l.PostedDate),
            })
            .OrderBy(r => r.FirstPosted)
            .ThenBy(r => r.SettlementId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        // The filter above excludes null order ids, so the "!" only quiets the compiler.
        return rows.Select(r => new OrderSettlementAmounts(r.AmazonOrderId!, r.SettlementId, r.Currency, r.Sales, r.Expenses, r.Refunds, r.Reserve)).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OrderExportRow>> GetExportRowsAsync(string marketplaceId, PageRequest request, OrderListFilter filter, int maxRows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filter);

        var rows =
            from s in Filter(marketplaceId, request, filter)
            join o in dbContext.Orders on new { s.MarketplaceId, s.AmazonOrderId } equals new { o.MarketplaceId, o.AmazonOrderId }
            join i in dbContext.OrderItems on o.Id equals i.OrderId
            join p in dbContext.Products on i.Sku equals p.Sku into products
            from p in products.DefaultIfEmpty()
            join c in dbContext.ProductCosts.Where(c => c.MarketplaceId == marketplaceId) on i.Sku equals c.Sku into costs
            from c in costs.DefaultIfEmpty()
            select new
            {
                s.AmazonOrderId,
                s.PurchaseDate,
                s.OrderStatus,
                s.Currency,
                s.Units,
                s.OrderTotal,
                i.Sku,
                Asin = p == null ? null : p.Asin,
                Title = p == null ? null : p.Title,
                i.Quantity,
                i.ItemPrice,
                UnitCost = c == null ? (decimal?)null : c.CostOfGoods,
            };

        var descending = filter.Sort.IsDescendingByDefault() != filter.Descending;
        var ordered = filter.Sort switch
        {
            OrderSort.Units => rows.OrderBy(r => r.Units, descending).ThenBy(r => r.PurchaseDate, true),
            OrderSort.Sales => rows.OrderBy(r => r.OrderTotal, descending).ThenBy(r => r.PurchaseDate, true),
            _ => rows.OrderBy(r => r.PurchaseDate, descending),
        };

        var page = await ordered
            .ThenBy(r => r.AmazonOrderId)
            .ThenBy(r => r.Sku)
            .Take(maxRows)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return page.Select(r => new OrderExportRow(r.AmazonOrderId, r.PurchaseDate, r.OrderStatus, r.Currency, r.Sku, r.Asin, r.Title, r.Quantity, r.ItemPrice, r.UnitCost)).ToList();
    }

    /// <summary>The marketplace's orders that match the search, purchase dates, and status.</summary>
    private IQueryable<OrderSummary> Filter(string marketplaceId, PageRequest request, OrderListFilter filter)
    {
        var query = dbContext.OrderSummaries.AsNoTracking().Where(o => o.MarketplaceId == marketplaceId);
        if (filter.PurchasedFrom is { } from)
        {
            query = query.Where(o => o.PurchaseDate >= from);
        }

        if (filter.PurchasedBefore is { } before)
        {
            query = query.Where(o => o.PurchaseDate < before);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(o => o.OrderStatus == status);
        }

        foreach (var term in ProductSearch.Terms(request.Search))
        {
            // Each word may match the order id or any of the order's items, so "B07 blue" finds an order
            // with an item whose ASIN starts B07 and another whose title says blue.
            var byItem =
                from o in dbContext.Orders
                where o.MarketplaceId == marketplaceId
                join i in dbContext.OrderItems on o.Id equals i.OrderId
                join p in dbContext.Products on i.Sku equals p.Sku into products
                from p in products.DefaultIfEmpty()
                where i.Sku.Contains(term) || (p != null && ((p.Asin != null && p.Asin.Contains(term)) || (p.Title != null && p.Title.Contains(term))))
                select o.AmazonOrderId;
            query = query.Where(o => o.AmazonOrderId.Contains(term) || byItem.Contains(o.AmazonOrderId));
        }

        return query;
    }

    /// <summary>Applies the list's sort, with a stable tie-break so pages never overlap.</summary>
    private static IOrderedQueryable<OrderSummary> Sorted(IQueryable<OrderSummary> query, OrderListFilter filter)
    {
        var descending = filter.Sort.IsDescendingByDefault() != filter.Descending;
        var ordered = filter.Sort switch
        {
            OrderSort.Units => query.OrderBy(o => o.Units, descending).ThenBy(o => o.PurchaseDate, true),
            OrderSort.Sales => query.OrderBy(o => o.OrderTotal, descending).ThenBy(o => o.PurchaseDate, true),
            _ => query.OrderBy(o => o.PurchaseDate, descending),
        };
        return ordered.ThenBy(o => o.AmazonOrderId);
    }
}
