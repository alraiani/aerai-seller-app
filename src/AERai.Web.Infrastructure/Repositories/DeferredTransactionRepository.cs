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
        string marketplaceId, IReadOnlyCollection<DeferredTransactionItem> stillDeferred, DateTimeOffset seenAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(stillDeferred);

        var incoming = stillDeferred.ToDictionary(i => i.TransactionId, StringComparer.Ordinal);
        if (incoming.Count != stillDeferred.Count)
        {
            throw new ArgumentException("Each transaction may appear only once.", nameof(stillDeferred));
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            // What is stored as held now, plus any incoming transaction stored earlier (Amazon could defer a
            // released one again, e.g. after an adjustment).
            var stored = await dbContext.DeferredTransactions
                .Where(t => t.MarketplaceId == marketplaceId && t.Status == DeferredTransactionStatus.Deferred)
                .ToDictionaryAsync(t => t.TransactionId, StringComparer.Ordinal, cancellationToken)
                .ConfigureAwait(false);
            foreach (var chunk in incoming.Keys.Where(id => !stored.ContainsKey(id)).Chunk(LookupChunkSize))
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

            foreach (var item in incoming.Values)
            {
                if (!stored.TryGetValue(item.TransactionId, out var row))
                {
                    row = new DeferredTransaction { TransactionId = item.TransactionId, MarketplaceId = marketplaceId, FirstSeenAt = seenAt };
                    dbContext.DeferredTransactions.Add(row);
                }

                row.AmazonOrderId = item.AmazonOrderId;
                row.TransactionType = item.TransactionType;
                row.Description = item.Description;
                row.PostedDate = item.PostedDate;
                row.Amount = item.Amount;
                row.Currency = item.Currency;
                row.DeferralReason = item.DeferralReason;
                row.MaturityDate = item.MaturityDate;
                row.Status = DeferredTransactionStatus.Deferred;
                row.ReleasedAt = null;
                row.LastSeenAt = seenAt;
            }

            // The listing is complete, so a held transaction it no longer has was released. Amazon releases on the
            // maturity date, so that is the release date once it has passed; otherwise when it was found gone.
            var released = 0;
            foreach (var row in stored.Values.Where(r => r.Status == DeferredTransactionStatus.Deferred && !incoming.ContainsKey(r.TransactionId)))
            {
                row.Status = DeferredTransactionStatus.Released;
                row.ReleasedAt = row.MaturityDate is { } maturity && maturity <= seenAt ? maturity : seenAt;
                released++;
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            return new DeferredApplyResult(incoming.Count, released);
        }).ConfigureAwait(false);
    }

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
    public async Task<PagedResult<DeferredPaymentRow>> GetPageAsync(
        string marketplaceId, PageRequest request, DeferredListFilter filter, DateTimeOffset releasedSince, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filter);

        var query = dbContext.DeferredTransactions.AsNoTracking().Where(t => t.MarketplaceId == marketplaceId);
        query = filter.View == DeferredView.Released
            ? query.Where(t => t.Status == DeferredTransactionStatus.Released && t.ReleasedAt >= releasedSince)
            : query.Where(t => t.Status == DeferredTransactionStatus.Deferred);
        foreach (var term in ProductSearch.Terms(request.Search))
        {
            query = query.Where(t => (t.AmazonOrderId != null && t.AmazonOrderId.Contains(term))
                || (t.Description != null && t.Description.Contains(term))
                || t.TransactionId.Contains(term));
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
