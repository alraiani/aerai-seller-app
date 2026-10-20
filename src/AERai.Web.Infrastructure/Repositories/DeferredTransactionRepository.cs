using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Products;
using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IDeferredTransactionRepository"/> and <see cref="IDeferredPaymentQueries"/>.</summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class DeferredTransactionRepository(AppDbContext dbContext) : IDeferredTransactionRepository, IDeferredPaymentQueries
{
    /// <summary>Ids per <c>IN</c> list, well under SQL Server's 2,100-parameter limit.</summary>
    private const int LookupChunkSize = 1_000;

    /// <inheritdoc/>
    public async Task<DeferredApplyResult> ApplyAsync(
        string marketplaceId,
        IReadOnlyCollection<AmazonDeferredTransaction> stillDeferred,
        IReadOnlyCollection<AmazonDeferredTransaction> released,
        DateTimeOffset seenAt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(stillDeferred);
        ArgumentNullException.ThrowIfNull(released);

        var held = stillDeferred.ToDictionary(i => i.TransactionId, StringComparer.Ordinal);
        if (held.Count != stillDeferred.Count)
        {
            throw new ArgumentException("Each transaction may appear only once.", nameof(stillDeferred));
        }

        // A transaction in both listings (Amazon updating between the two calls) is still held.
        var releasedNow = released
            .Where(i => !held.ContainsKey(i.TransactionId))
            .GroupBy(i => i.TransactionId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            // What is stored as held now, plus every listed transaction stored earlier.
            var stored = await dbContext.DeferredTransactions
                .Where(t => t.MarketplaceId == marketplaceId && t.Status == DeferredTransactionStatus.Deferred)
                .ToDictionaryAsync(t => t.TransactionId, StringComparer.Ordinal, cancellationToken)
                .ConfigureAwait(false);
            foreach (var chunk in held.Keys.Concat(releasedNow.Keys).Where(id => !stored.ContainsKey(id)).Chunk(LookupChunkSize))
            {
                var earlier = await dbContext.DeferredTransactions
                    .Where(t => chunk.Contains(t.TransactionId))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                foreach (var row in earlier)
                {
                    stored[row.TransactionId] = row;
                }
            }

            var refreshed = new List<(DeferredTransaction Row, AmazonDeferredTransaction Item)>();
            foreach (var item in held.Values)
            {
                var row = Upsert(stored, item, marketplaceId, seenAt);
                row.Status = DeferredTransactionStatus.Deferred;
                row.ReleasedAt = null;
                refreshed.Add((row, item));
            }

            var newlyReleased = 0;
            foreach (var item in releasedNow.Values)
            {
                // A release Amazon listed before is history and does not change; rewriting it daily would only cost.
                if (stored.TryGetValue(item.TransactionId, out var known) && known.Status == DeferredTransactionStatus.Released && known.SettlementId is not null)
                {
                    continue;
                }

                var wasHeld = known?.Status == DeferredTransactionStatus.Deferred;
                var row = Upsert(stored, item, marketplaceId, seenAt);
                row.Status = DeferredTransactionStatus.Released;
                row.ReleasedAt ??= ReleaseDate(row.MaturityDate, seenAt);
                refreshed.Add((row, item));
                newlyReleased += wasHeld ? 1 : 0;
            }

            // Both listings are complete, so a held transaction in neither was released.
            foreach (var row in stored.Values.Where(r => r.Status == DeferredTransactionStatus.Deferred
                && !held.ContainsKey(r.TransactionId) && !releasedNow.ContainsKey(r.TransactionId)))
            {
                row.Status = DeferredTransactionStatus.Released;
                row.ReleasedAt = ReleaseDate(row.MaturityDate, seenAt);
                newlyReleased++;
            }

            // Products are replaced for every transaction written from a listing.
            foreach (var chunk in refreshed.Select(r => r.Row.TransactionId).Chunk(LookupChunkSize))
            {
                await dbContext.DeferredTransactionItems
                    .Where(i => chunk.Contains(i.TransactionId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            dbContext.DeferredTransactionItems.AddRange(refreshed.SelectMany(r => r.Item.Products.Select(p => new DeferredTransactionItem
            {
                TransactionId = r.Row.TransactionId,
                Sku = p.Sku,
                Asin = p.Asin,
                Quantity = p.Quantity,
                Principal = p.Principal,
                Amount = p.Amount,
            })));

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            return new DeferredApplyResult(held.Count, releasedNow.Count, newlyReleased);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<bool> HasReleaseHistoryAsync(string marketplaceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);

        // Releases Amazon listed carry their settlement; ones the sync only inferred may not.
        return dbContext.DeferredTransactions.AsNoTracking()
            .AnyAsync(t => t.MarketplaceId == marketplaceId && t.Status == DeferredTransactionStatus.Released && t.SettlementId != null, cancellationToken);
    }

    /// <summary>Adds the transaction or refreshes the stored one with what Amazon listed.</summary>
    private DeferredTransaction Upsert(Dictionary<string, DeferredTransaction> stored, AmazonDeferredTransaction item, string marketplaceId, DateTimeOffset seenAt)
    {
        if (!stored.TryGetValue(item.TransactionId, out var row))
        {
            row = new DeferredTransaction { TransactionId = item.TransactionId, MarketplaceId = marketplaceId, FirstSeenAt = seenAt };
            dbContext.DeferredTransactions.Add(row);
            stored[item.TransactionId] = row;
        }

        row.AmazonOrderId = item.AmazonOrderId;
        row.SettlementId = item.SettlementId;
        row.TransactionType = item.TransactionType;
        row.Description = item.Description;
        row.PostedDate = item.PostedDate;
        row.Amount = item.Amount;
        row.Currency = item.Currency;
        row.DeferralReason = item.DeferralReason;
        row.MaturityDate = item.MaturityDate;
        row.LastSeenAt = seenAt;
        return row;
    }

    /// <summary>Amazon releases on the maturity date, so that is the release date once it has passed; otherwise when the sync saw it.</summary>
    private static DateTimeOffset ReleaseDate(DateTimeOffset? maturity, DateTimeOffset seenAt) =>
        maturity is { } date && date <= seenAt ? date : seenAt;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DeferredHeld>> GetHeldAsync(string marketplaceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        var rows = await dbContext.DeferredTransactions.AsNoTracking()
            .Where(t => t.MarketplaceId == marketplaceId && t.Status == DeferredTransactionStatus.Deferred)
            .GroupBy(t => t.Currency ?? string.Empty)
            .Select(g => new
            {
                Currency = g.Key,
                Amount = g.Sum(t => t.Amount),
                Transactions = g.Count(),
                Orders = g.Where(t => t.AmazonOrderId != null).Select(t => t.AmazonOrderId).Distinct().Count(),
                NextMaturity = g.Min(t => t.MaturityDate),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(r => new DeferredHeld(r.Currency, r.Amount, r.Transactions, r.Orders, r.NextMaturity))];
    }

    /// <inheritdoc/>
    public async Task<DeferredSyncState> GetSyncStateAsync(string marketplaceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        var schedules = await dbContext.SyncSchedules.AsNoTracking()
            .Where(s => s.MarketplaceId == marketplaceId && s.ReportType == AmazonReportType.DeferredTransactions)
            .Select(s => s.IsEnabled)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // A run that read the whole listing finished as Succeeded, or NoData when Amazon held nothing.
        var lastSuccess = await dbContext.SyncRuns.AsNoTracking()
            .Where(r => r.MarketplaceId == marketplaceId && r.ReportType == AmazonReportType.DeferredTransactions
                && (r.Status == SyncRunStatus.Succeeded || r.Status == SyncRunStatus.NoData))
            .MaxAsync(r => r.CompletedAt, cancellationToken)
            .ConfigureAwait(false);
        return new DeferredSyncState(schedules.Count > 0, schedules.Contains(true), lastSuccess);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<DeferredPaymentRow>> GetPageAsync(string marketplaceId, PageRequest request, DeferredListFilter filter, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filter);

        var query = Selected(marketplaceId, filter.View, request.Search);
        if (filter.SettlementId is { } settlementId)
        {
            query = settlementId == DeferredListFilter.NoSettlement
                ? query.Where(t => t.SettlementId == null)
                : query.Where(t => t.SettlementId == settlementId);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var descending = filter.Descending;
        IOrderedQueryable<DeferredTransaction> ordered = filter.Sort switch
        {
            // Largest first and newest first are the natural directions.
            DeferredSort.Amount => descending ? query.OrderBy(t => t.Amount) : query.OrderByDescending(t => t.Amount),
            DeferredSort.Posted => descending ? query.OrderBy(t => t.PostedDate) : query.OrderByDescending(t => t.PostedDate),
            // Soonest release first, with unknown dates last.
            _ => descending
                ? query.OrderBy(t => t.MaturityDate == null ? 0 : 1).ThenByDescending(t => t.MaturityDate)
                : query.OrderBy(t => t.MaturityDate == null ? 1 : 0).ThenBy(t => t.MaturityDate),
        };
        var rows = await ordered.ThenBy(t => t.TransactionId)
            .Skip(request.Skip)
            .Take(request.SafePageSize)
            .Select(t => new DeferredPaymentRow(
                t.TransactionId, t.AmazonOrderId, t.Description, t.DeferralReason, t.PostedDate, t.MaturityDate, t.Amount, t.Currency, t.Status, t.ReleasedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PagedResult<DeferredPaymentRow>(rows, total, request.SafePage, request.SafePageSize);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<DeferredSettlementGroup>> GetGroupsAsync(
        string marketplaceId, string listMarketplaceId, PageRequest request, DeferredView view, int maxRowsPerGroup, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(listMarketplaceId);
        ArgumentNullException.ThrowIfNull(request);

        var selected = Selected(marketplaceId, view, request.Search);
        var totals = selected
            .GroupBy(t => new { t.SettlementId, Currency = t.Currency ?? string.Empty })
            .Select(g => new
            {
                g.Key.SettlementId,
                g.Key.Currency,
                Amount = g.Sum(t => t.Amount),
                Count = g.Count(),
                Orders = g.Where(t => t.AmazonOrderId != null).Select(t => t.AmazonOrderId).Distinct().Count(),
                HeldAmount = g.Sum(t => t.Status == DeferredTransactionStatus.Deferred ? t.Amount : 0m),
                HeldCount = g.Count(t => t.Status == DeferredTransactionStatus.Deferred),
                FirstRelease = g.Min(t => t.MaturityDate),
                LastRelease = g.Max(t => t.MaturityDate),
                FirstPosted = g.Min(t => t.PostedDate),
                LastPosted = g.Max(t => t.PostedDate),
            });
        var withPeriods =
            from g in totals
            join s in dbContext.Settlements.AsNoTracking() on g.SettlementId equals s.SettlementId into settlements
            from s in settlements.DefaultIfEmpty()
            select new { Group = g, PeriodStart = (DateTimeOffset?)s.PeriodStart, PeriodEnd = (DateTimeOffset?)s.PeriodEnd, ListedIn = s.MarketplaceId };

        var total = await withPeriods.CountAsync(cancellationToken).ConfigureAwait(false);

        // Newest first: a settlement's period end, or for one not imported (the open one, whose sales are the latest)
        // its latest sale.
        var page = await withPeriods
            .OrderByDescending(x => x.PeriodEnd ?? x.Group.LastPosted)
            .ThenBy(x => x.Group.SettlementId)
            .Skip(request.Skip)
            .Take(request.SafePageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Each group's first rows, soonest release first; one query for the whole page.
        var ids = page.Where(x => x.Group.SettlementId != null).Select(x => x.Group.SettlementId!).ToList(); // Non-null: filtered just before.
        var withoutSettlement = page.Any(x => x.Group.SettlementId == null);
        var rows = await selected
            .Where(t => (t.SettlementId != null && ids.Contains(t.SettlementId)) || (withoutSettlement && t.SettlementId == null))
            .OrderBy(t => t.MaturityDate == null ? 1 : 0)
            .ThenBy(t => t.MaturityDate)
            .ThenBy(t => t.TransactionId)
            .Select(t => new
            {
                t.SettlementId,
                Row = new DeferredPaymentRow(
                    t.TransactionId, t.AmazonOrderId, t.Description, t.DeferralReason, t.PostedDate, t.MaturityDate, t.Amount, t.Currency, t.Status, t.ReleasedAt),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var bySettlement = rows.ToLookup(r => (r.SettlementId ?? string.Empty, r.Row.Currency ?? string.Empty), r => r.Row);

        var groups = page.Select(x => new DeferredSettlementGroup(
                x.Group.SettlementId,
                x.Group.Currency,
                x.Group.Amount,
                x.Group.Count,
                x.Group.Orders,
                x.Group.HeldAmount,
                x.Group.HeldCount,
                x.Group.FirstRelease,
                x.Group.LastRelease,
                x.Group.FirstPosted,
                x.Group.LastPosted,
                x.PeriodStart,
                x.PeriodEnd,
                x.ListedIn == listMarketplaceId,
                [.. bySettlement[(x.Group.SettlementId ?? string.Empty, x.Group.Currency)].Take(maxRowsPerGroup)]))
            .ToList();
        return new PagedResult<DeferredSettlementGroup>(groups, total, request.SafePage, request.SafePageSize);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SettlementDeferral>> GetBySettlementsAsync(
        IReadOnlyCollection<string> marketplaceIds, IReadOnlyCollection<string> settlementIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplaceIds);
        ArgumentNullException.ThrowIfNull(settlementIds);
        if (marketplaceIds.Count == 0 || settlementIds.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.DeferredTransactions.AsNoTracking()
            .Where(t => marketplaceIds.Contains(t.MarketplaceId) && t.SettlementId != null && settlementIds.Contains(t.SettlementId))
            .GroupBy(t => new { t.SettlementId, Currency = t.Currency ?? string.Empty })
            .Select(g => new
            {
                g.Key.SettlementId,
                g.Key.Currency,
                Amount = g.Sum(t => t.Amount),
                Count = g.Count(),
                Orders = g.Where(t => t.AmazonOrderId != null).Select(t => t.AmazonOrderId).Distinct().Count(),
                HeldAmount = g.Sum(t => t.Status == DeferredTransactionStatus.Deferred ? t.Amount : 0m),
                HeldCount = g.Count(t => t.Status == DeferredTransactionStatus.Deferred),
                FirstRelease = g.Min(t => t.MaturityDate),
                LastRelease = g.Max(t => t.MaturityDate),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(r => new SettlementDeferral(
            r.SettlementId!, // Non-null: filtered in the query.
            r.Currency, r.Amount, r.Count, r.Orders, r.HeldAmount, r.HeldCount, r.FirstRelease, r.LastRelease))];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SkuDeferral>> GetSkuDeferralsAsync(IReadOnlyCollection<string> marketplaceIds, string settlementId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplaceIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(settlementId);

        var rows = await SettlementItems(marketplaceIds, settlementId)
            .GroupBy(x => new { x.Item.Sku, x.PriceEach, Currency = x.Transaction.Currency ?? string.Empty })
            .Select(g => new
            {
                g.Key.Sku,
                g.Key.PriceEach,
                g.Key.Currency,
                Quantity = g.Sum(x => x.Item.Quantity),
                Amount = g.Sum(x => x.Item.Amount),
                Orders = g.Where(x => x.Transaction.AmazonOrderId != null).Select(x => x.Transaction.AmazonOrderId).Distinct().Count(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(r => new SkuDeferral(r.Sku, r.PriceEach, r.Quantity, r.Amount, r.Orders, r.Currency))];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DeferredLineItem>> GetSkuLinesAsync(
        IReadOnlyCollection<string> marketplaceIds, string settlementId, string? sku, decimal? priceEach, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplaceIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(settlementId);

        return await SettlementItems(marketplaceIds, settlementId)
            .Where(x => x.Item.Sku == sku && x.PriceEach == priceEach)
            .OrderBy(x => x.Transaction.MaturityDate == null ? 1 : 0)
            .ThenBy(x => x.Transaction.MaturityDate)
            .ThenBy(x => x.Transaction.AmazonOrderId)
            .Select(x => new DeferredLineItem(
                x.Transaction.TransactionId,
                x.Transaction.AmazonOrderId,
                x.Item.Quantity,
                x.Item.Amount,
                x.Transaction.Currency ?? string.Empty,
                x.Transaction.DeferralReason,
                x.Transaction.MaturityDate,
                x.Transaction.Status,
                x.Transaction.ReleasedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The deferred products sold in a settlement, with each one's price each: product charges ÷ quantity, rounded to
    /// the cent like <c>rpt.vw_SettlementLine.SalePrice</c> (refunds carry negative charges, hence the absolute value).
    /// </summary>
    private IQueryable<SettlementItem> SettlementItems(IReadOnlyCollection<string> marketplaceIds, string settlementId) =>
        from t in dbContext.DeferredTransactions.AsNoTracking()
        where marketplaceIds.Contains(t.MarketplaceId) && t.SettlementId == settlementId
        join i in dbContext.DeferredTransactionItems.AsNoTracking() on t.TransactionId equals i.TransactionId
        select new SettlementItem { Transaction = t, Item = i, PriceEach = i.Quantity > 0 ? Math.Round(Math.Abs(i.Principal) / i.Quantity, 2) : null };

    /// <summary>A marketplace's transactions that a view and search select.</summary>
    private IQueryable<DeferredTransaction> Selected(string marketplaceId, DeferredView view, string? search)
    {
        var query = dbContext.DeferredTransactions.AsNoTracking().Where(t => t.MarketplaceId == marketplaceId);
        query = view switch
        {
            DeferredView.Released => query.Where(t => t.Status == DeferredTransactionStatus.Released),
            DeferredView.All => query,
            _ => query.Where(t => t.Status == DeferredTransactionStatus.Deferred),
        };
        foreach (var term in ProductSearch.Terms(search))
        {
            query = query.Where(t => (t.AmazonOrderId != null && t.AmazonOrderId.Contains(term))
                || (t.Description != null && t.Description.Contains(term))
                || t.TransactionId.Contains(term));
        }

        return query;
    }

    /// <summary>A deferred product with its transaction and price each (an initializer, which EF can keep querying).</summary>
    private sealed class SettlementItem
    {
        public required DeferredTransaction Transaction { get; init; }

        public required DeferredTransactionItem Item { get; init; }

        public decimal? PriceEach { get; init; }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DeferredPaymentRow>> GetReleasedInSettlementAsync(string listMarketplaceId, string settlementId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listMarketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(settlementId);

        // The settlements list holds every marketplace listed under it (Canada's payouts are in the United
        // States list), so its orders may be any of theirs.
        var listed = dbContext.Marketplaces
            .Where(m => m.MarketplaceId == listMarketplaceId || m.SettlementMarketplaceId == listMarketplaceId)
            .Select(m => m.MarketplaceId);
        var orders = dbContext.SettlementLines
            .Where(l => l.SettlementId == settlementId && l.TransactionType == "Order" && l.AmazonOrderId != null
                && dbContext.Settlements.Any(s => s.SettlementId == settlementId && s.MarketplaceId == listMarketplaceId))
            .Select(l => l.AmazonOrderId);
        return await dbContext.DeferredTransactions.AsNoTracking()
            .Where(t => t.Status == DeferredTransactionStatus.Released && listed.Contains(t.MarketplaceId) && orders.Contains(t.AmazonOrderId))
            .Select(t => new DeferredPaymentRow(
                t.TransactionId, t.AmazonOrderId, t.Description, t.DeferralReason, t.PostedDate, t.MaturityDate, t.Amount, t.Currency, t.Status, t.ReleasedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DeferredPaymentRow>> GetByOrdersAsync(IReadOnlyCollection<string> marketplaceIds, IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplaceIds);
        ArgumentNullException.ThrowIfNull(orderIds);
        if (marketplaceIds.Count == 0 || orderIds.Count == 0)
        {
            return [];
        }

        return await dbContext.DeferredTransactions.AsNoTracking()
            .Where(t => marketplaceIds.Contains(t.MarketplaceId) && t.AmazonOrderId != null && orderIds.Contains(t.AmazonOrderId))
            .Select(t => new DeferredPaymentRow(
                t.TransactionId, t.AmazonOrderId, t.Description, t.DeferralReason, t.PostedDate, t.MaturityDate, t.Amount, t.Currency, t.Status, t.ReleasedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
