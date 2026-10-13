using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Reporting;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Queries;

/// <summary>
/// EF Core implementation of <see cref="IReportingQueries"/> over the <c>rpt</c> views.
/// </summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class ReportingQueries(AppDbContext dbContext) : IReportingQueries
{
    /// <inheritdoc/>
    public Task<PagedResult<OrderSummary>> GetOrdersAsync(string marketplaceId, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.OrderSummaries.AsNoTracking().Where(v => v.MarketplaceId == marketplaceId);
        if (request.SafeSearch is { } search)
        {
            query = query.Where(o => o.AmazonOrderId.Contains(search) || o.OrderStatus.Contains(search));
        }

        return query
            .OrderByDescending(o => o.PurchaseDate)
            .ThenBy(o => o.AmazonOrderId)
            .ToPagedResultAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PagedResult<SettlementSummary>> GetSettlementsAsync(string marketplaceId, PageRequest request, SettlementListFilter? filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        filter ??= SettlementListFilter.Default;

        var query = FilterSettlements(marketplaceId, request, filter);
        var descending = filter.Sort.IsDescendingByDefault() != filter.Descending;
        var ordered = filter.Sort switch
        {
            SettlementSort.Sales => query.OrderBy(s => s.Sales, descending),
            SettlementSort.Fees => query.OrderBy(s => s.Fees, descending),
            SettlementSort.Refunds => query.OrderBy(s => s.Refunds, descending),
            SettlementSort.Net => query.OrderBy(s => s.Net, descending),
            _ => query.OrderBy(s => s.PeriodEnd, descending),
        };

        return ordered
            .ThenByDescending(s => s.PeriodEnd)
            .ThenBy(s => s.SettlementId)
            .ToPagedResultAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SettlementTotals> GetSettlementTotalsAsync(string marketplaceId, PageRequest request, SettlementListFilter? filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var totals = await FilterSettlements(marketplaceId, request, filter ?? SettlementListFilter.Default)
            .GroupBy(_ => 1)
            .Select(g => new SettlementTotals(g.Count(), g.Sum(s => s.Sales), g.Sum(s => s.Fees), g.Sum(s => s.Refunds), g.Sum(s => s.Other), g.Sum(s => s.Net)))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return totals ?? SettlementTotals.Empty;
    }

    /// <summary>The settlements a list search and period range select, unordered.</summary>
    private IQueryable<SettlementSummary> FilterSettlements(string marketplaceId, PageRequest request, SettlementListFilter filter)
    {
        var query = dbContext.SettlementSummaries.AsNoTracking().Where(v => v.MarketplaceId == marketplaceId);
        if (filter.PeriodEndFrom is { } from)
        {
            query = query.Where(s => s.PeriodEnd >= from);
        }

        if (filter.PeriodEndBefore is { } before)
        {
            query = query.Where(s => s.PeriodEnd < before);
        }

        if (request.SafeSearch is { } search)
        {
            // People look a payout up by what they know: the settlement id from the bank, an order id
            // from a customer, or a product. Orders and SKUs come from the lines, ASINs and titles from
            // the per-SKU summary (which joins the catalog).
            query = query.Where(s => s.SettlementId.Contains(search)
                || dbContext.SettlementLineDetails.Any(l => l.MarketplaceId == s.MarketplaceId && l.SettlementId == s.SettlementId
                    && ((l.AmazonOrderId != null && l.AmazonOrderId.Contains(search)) || (l.Sku != null && l.Sku.Contains(search))))
                || dbContext.SettlementSkuSummaries.Any(r => r.MarketplaceId == s.MarketplaceId && r.SettlementId == s.SettlementId
                    && ((r.Asin != null && r.Asin.Contains(search)) || (r.Title != null && r.Title.Contains(search)))));
        }

        return query;
    }

    /// <inheritdoc/>
    public Task<SettlementSummary?> GetSettlementAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken) =>
        dbContext.SettlementSummaries.AsNoTracking()
            .FirstOrDefaultAsync(s => s.MarketplaceId == marketplaceId && s.SettlementId == settlementId, cancellationToken);

    /// <inheritdoc/>
    public Task<PagedResult<SettlementSkuSummary>> GetSettlementSkuSummaryAsync(string marketplaceId, string settlementId, PageRequest request, SettlementSkuFilter? filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        filter ??= SettlementSkuFilter.Default;

        var query = dbContext.SettlementSkuSummaries.AsNoTracking()
            .Where(r => r.MarketplaceId == marketplaceId && r.SettlementId == settlementId);
        query = filter.Kind switch
        {
            SettlementRowKind.WithRefunds => query.Where(r => r.Refunds != 0),
            SettlementRowKind.NoSku => query.Where(r => r.Sku == null),
            _ => query,
        };

        if (request.SafeSearch is { } search)
        {
            query = query.Where(r => (r.Sku != null && r.Sku.Contains(search))
                || (r.Asin != null && r.Asin.Contains(search))
                || (r.Title != null && r.Title.Contains(search)));
        }

        var descending = filter.Sort.IsDescendingByDefault() != filter.Descending;
        var ordered = filter.Sort switch
        {
            SettlementSkuSort.Sku => query.OrderBy(r => r.Sku, descending),
            SettlementSkuSort.Units => query.OrderBy(r => r.Units, descending),
            SettlementSkuSort.Orders => query.OrderBy(r => r.OrderCount, descending),
            SettlementSkuSort.Fees => query.OrderBy(r => r.Fees, descending),
            SettlementSkuSort.Refunds => query.OrderBy(r => r.Refunds, descending),
            SettlementSkuSort.Net => query.OrderBy(r => r.Net, descending),
            _ => query.OrderBy(r => r.Sales, descending),
        };

        return ordered
            .ThenBy(r => r.Sku)
            .ThenBy(r => r.SalePrice)
            .ToPagedResultAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SettlementSkuSummary?> GetSettlementSkuGroupAsync(string marketplaceId, string settlementId, string? sku, decimal? salePrice, CancellationToken cancellationToken) =>
        dbContext.SettlementSkuSummaries.AsNoTracking()
            .FirstOrDefaultAsync(r => r.MarketplaceId == marketplaceId && r.SettlementId == settlementId && r.Sku == sku && r.SalePrice == salePrice, cancellationToken);

    /// <inheritdoc/>
    public async Task<PagedResult<SettlementOrderGroup>> GetSettlementOrderGroupsAsync(
        string marketplaceId, string settlementId, string? sku, decimal? salePrice, PageRequest request, SettlementLineFilter? filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        filter ??= SettlementLineFilter.Default;

        var lines = dbContext.SettlementLineDetails.AsNoTracking()
            .Where(l => l.MarketplaceId == marketplaceId && l.SettlementId == settlementId && l.Sku == sku && l.SalePrice == salePrice);

        // A group is one order; lines without an order (service fees and the like) are grouped by
        // transaction type instead, so the key's second part is set only when the first is null.
        // Search and the bucket filter pick groups, never single lines, so a listed group's lines
        // always add up to its total.
        var search = request.SafeSearch;
        var bucket = filter.Bucket;
        var groups = lines
            .GroupBy(l => new { l.AmazonOrderId, NoOrderType = l.AmazonOrderId == null ? l.TransactionType : null })
            .Select(g => new GroupRow
            {
                AmazonOrderId = g.Key.AmazonOrderId,
                TransactionType = g.Key.NoOrderType,
                FirstPosted = g.Min(l => l.PostedDate),
                Units = g.Sum(l => l.Units),
                Amount = g.Sum(l => l.Amount),
                Sales = g.Sum(l => l.Sales),
                Fees = g.Sum(l => l.Fees),
                Refunds = g.Sum(l => l.Refunds),
                Other = g.Sum(l => l.Other),
                SearchHits = search == null ? 1 : g.Count(l => (l.AmazonOrderId != null && l.AmazonOrderId.Contains(search))
                    || l.TransactionType.Contains(search)
                    || (l.AmountType != null && l.AmountType.Contains(search))
                    || (l.AmountDescription != null && l.AmountDescription.Contains(search))),
                BucketHits = bucket == null ? 1 : g.Count(l =>
                    (bucket == SettlementBucket.Sales && l.Sales != 0)
                    || (bucket == SettlementBucket.Fees && l.Fees != 0)
                    || (bucket == SettlementBucket.Refunds && l.Refunds != 0)
                    || (bucket == SettlementBucket.Other && l.Other != 0)),
            })
            .Where(g => g.SearchHits > 0 && g.BucketHits > 0);

        var descending = filter.Sort.IsDescendingByDefault() != filter.Descending;
        var ordered = filter.Sort switch
        {
            SettlementGroupSort.Amount => groups.OrderBy(g => g.Amount, descending),
            SettlementGroupSort.Order => groups.OrderBy(g => g.AmazonOrderId == null ? 1 : 0).ThenBy(g => g.AmazonOrderId, descending),
            _ => groups.OrderBy(g => g.FirstPosted, descending),
        };

        var page = await ordered
            .ThenBy(g => g.AmazonOrderId)
            .ThenBy(g => g.TransactionType)
            .ToPagedResultAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (page.Items.Count == 0)
        {
            return new PagedResult<SettlementOrderGroup>([], page.TotalCount, page.Page, page.PageSize);
        }

        // Second round trip: every line of this page's groups (not one query per group).
        var orderIds = page.Items.Where(g => g.AmazonOrderId != null).Select(g => g.AmazonOrderId!).ToList();
        var noOrderTypes = page.Items.Where(g => g.AmazonOrderId == null).Select(g => g.TransactionType).ToList();
        var pageLines = await lines
            .Where(l => (l.AmazonOrderId != null && orderIds.Contains(l.AmazonOrderId))
                || (l.AmazonOrderId == null && noOrderTypes.Contains(l.TransactionType)))
            .OrderBy(l => l.PostedDate)
            .ThenBy(l => l.LineId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byOrder = pageLines.Where(l => l.AmazonOrderId != null).ToLookup(l => l.AmazonOrderId!, StringComparer.Ordinal);
        var byType = pageLines.Where(l => l.AmazonOrderId == null).ToLookup(l => l.TransactionType, StringComparer.Ordinal);

        var items = page.Items
            .Select(g => new SettlementOrderGroup(
                g.AmazonOrderId,
                g.TransactionType,
                g.FirstPosted,
                g.Units,
                g.Amount,
                g.Sales,
                g.Fees,
                g.Refunds,
                g.Other,
                (g.AmazonOrderId is { } orderId ? byOrder[orderId] : byType[g.TransactionType ?? string.Empty]).ToList()))
            .ToList();
        return new PagedResult<SettlementOrderGroup>(items, page.TotalCount, page.Page, page.PageSize);
    }

    /// <summary>One order group's totals as SQL Server computes them, before its lines are attached.</summary>
    private sealed class GroupRow
    {
        public string? AmazonOrderId { get; init; }

        public string? TransactionType { get; init; }

        public DateTimeOffset? FirstPosted { get; init; }

        public int Units { get; init; }

        public decimal Amount { get; init; }

        public decimal Sales { get; init; }

        public decimal Fees { get; init; }

        public decimal Refunds { get; init; }

        public decimal Other { get; init; }

        public int SearchHits { get; init; }

        public int BucketHits { get; init; }
    }
}
