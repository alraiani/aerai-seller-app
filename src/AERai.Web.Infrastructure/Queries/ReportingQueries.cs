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
    public async Task<PagedResult<SettlementSummary>> GetSettlementsAsync(string marketplaceId, PageRequest request, SettlementListFilter? filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        filter ??= SettlementListFilter.Default;

        var query = await FilterSettlementsAsync(marketplaceId, request, filter, cancellationToken).ConfigureAwait(false);
        var descending = filter.Sort.IsDescendingByDefault() != filter.Descending;
        // The list shows converted amounts, so it sorts by them; a settlement without a rate yet sorts
        // by what it was paid.
        var ordered = filter.Sort switch
        {
            SettlementSort.Sales => query.OrderBy(s => s.HomeSales ?? s.Sales, descending),
            SettlementSort.Fees => query.OrderBy(s => s.HomeFees ?? s.Fees, descending),
            SettlementSort.Refunds => query.OrderBy(s => s.HomeRefunds ?? s.Refunds, descending),
            SettlementSort.Net => query.OrderBy(s => s.HomeNet ?? s.Net, descending),
            _ => query.OrderBy(s => s.PeriodEnd, descending),
        };

        return await ordered
            .ThenByDescending(s => s.PeriodEnd)
            .ThenBy(s => s.SettlementId)
            .ToPagedResultAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SettlementTotals?> GetSettlementTotalsAsync(string marketplaceId, PageRequest request, SettlementListFilter? filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Every row of a marketplace has the same HomeCurrency, so this is one group (or none).
        var query = await FilterSettlementsAsync(marketplaceId, request, filter ?? SettlementListFilter.Default, cancellationToken).ConfigureAwait(false);
        return await query
            .GroupBy(s => s.HomeCurrency)
            .Select(g => new SettlementTotals(
                g.Key,
                g.Count(s => s.HomeNet != null),
                g.Count(s => s.HomeNet == null),
                g.Sum(s => s.HomeSales ?? 0),
                g.Sum(s => s.HomeFees ?? 0),
                g.Sum(s => s.HomeRefunds ?? 0),
                g.Sum(s => s.HomeOther ?? 0),
                g.Sum(s => s.HomeNet ?? 0)))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetSettlementCurrenciesAsync(string marketplaceId, PageRequest request, SettlementListFilter? filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = await FilterSettlementsAsync(marketplaceId, request, (filter ?? SettlementListFilter.Default) with { Currency = null }, cancellationToken).ConfigureAwait(false);
        var currencies = await query
            .Where(s => s.Currency != null)
            .GroupBy(s => s.Currency)
            .Select(g => new { Currency = g.Key!, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return currencies.OrderByDescending(c => c.Count).ThenBy(c => c.Currency, StringComparer.Ordinal).Select(c => c.Currency).ToList();
    }

    /// <summary>The settlements a list search, period range, and currency select, unordered.</summary>
    private async Task<IQueryable<SettlementSummary>> FilterSettlementsAsync(string marketplaceId, PageRequest request, SettlementListFilter filter, CancellationToken cancellationToken)
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

        if (filter.Currency is { } currency)
        {
            query = query.Where(s => s.Currency == currency);
        }

        if (request.SafeSearch is { } search)
        {
            var ids = await MatchingSettlementIdsAsync(marketplaceId, search, cancellationToken).ConfigureAwait(false);
            query = query.Where(s => ids.Contains(s.SettlementId));
        }

        return query;
    }

    /// <summary>
    /// The settlements a search term finds: by settlement id, or by an order id or product (SKU, ASIN,
    /// or title) on any of their lines — what people know when they look a payout up.
    /// </summary>
    /// <remarks>
    /// Run on its own against the core tables, then handed to the view as a short id list. Folding the
    /// same test into the view query made SQL Server recompute every line's price and bucket while
    /// scanning (seconds per query, and the list page runs three).
    /// </remarks>
    private async Task<List<string>> MatchingSettlementIdsAsync(string marketplaceId, string search, CancellationToken cancellationToken)
    {
        var products = dbContext.Products.AsNoTracking()
            .Where(p => p.Sku.Contains(search) || (p.Asin != null && p.Asin.Contains(search)) || (p.Title != null && p.Title.Contains(search)))
            .Select(p => p.Sku);
        // The navigation is a SQL join here, never a null dereference, so the "!" only quiets the compiler.
        var byLine = dbContext.SettlementLines.AsNoTracking()
            .Where(l => l.Settlement!.MarketplaceId == marketplaceId
                && ((l.AmazonOrderId != null && l.AmazonOrderId.Contains(search))
                    || (l.Sku != null && (l.Sku.Contains(search) || products.Contains(l.Sku)))))
            .Select(l => l.SettlementId);
        return await dbContext.Settlements.AsNoTracking()
            .Where(s => s.MarketplaceId == marketplaceId && s.SettlementId.Contains(search))
            .Select(s => s.SettlementId)
            .Union(byLine)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
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

        // Converted columns are NULL only while the settlement has no rate, and then it is shown as paid.
        var home = filter.Amounts == SettlementAmounts.Home;
        var descending = filter.Sort.IsDescendingByDefault() != filter.Descending;
        var ordered = filter.Sort switch
        {
            SettlementSkuSort.Sku => query.OrderBy(r => r.Sku, descending),
            SettlementSkuSort.Units => query.OrderBy(r => r.Units, descending),
            SettlementSkuSort.Orders => query.OrderBy(r => r.OrderCount, descending),
            SettlementSkuSort.Fees => query.OrderBy(r => home ? r.HomeFees ?? r.Fees : r.Fees, descending),
            SettlementSkuSort.Refunds => query.OrderBy(r => home ? r.HomeRefunds ?? r.Refunds : r.Refunds, descending),
            SettlementSkuSort.Net => query.OrderBy(r => home ? r.HomeNet ?? r.Net : r.Net, descending),
            _ => query.OrderBy(r => home ? r.HomeSales ?? r.Sales : r.Sales, descending),
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
        var home = filter.Amounts == SettlementAmounts.Home;
        var groups = lines
            .GroupBy(l => new { l.AmazonOrderId, NoOrderType = l.AmazonOrderId == null ? l.TransactionType : null })
            .Select(g => new GroupRow
            {
                AmazonOrderId = g.Key.AmazonOrderId,
                TransactionType = g.Key.NoOrderType,
                FirstPosted = g.Min(l => l.PostedDate),
                Units = g.Sum(l => l.Units),
                Amount = g.Sum(l => home ? l.HomeAmount ?? l.Amount : l.Amount),
                Sales = g.Sum(l => home ? l.HomeSales ?? l.Sales : l.Sales),
                Fees = g.Sum(l => home ? l.HomeFees ?? l.Fees : l.Fees),
                Refunds = g.Sum(l => home ? l.HomeRefunds ?? l.Refunds : l.Refunds),
                Other = g.Sum(l => home ? l.HomeOther ?? l.Other : l.Other),
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
        var currency = pageLines.Count == 0 ? null : SettlementMoney.AmountOf(pageLines[0], filter.Amounts).Currency;

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
                currency,
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
