using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Bookkeeping;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IBookkeepingQueries"/> and <see cref="IBookkeepingStore"/>.</summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class BookkeepingRepository(AppDbContext dbContext) : IBookkeepingQueries, IBookkeepingStore
{
    /// <inheritdoc/>
    public async Task<SettlementBooks?> GetSettlementAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken)
    {
        var settlement = await (
                from s in dbContext.Settlements.AsNoTracking()
                join m in dbContext.Marketplaces.AsNoTracking() on s.MarketplaceId equals m.MarketplaceId
                where s.SettlementId == settlementId && s.MarketplaceId == marketplaceId
                select new
                {
                    s.SettlementId,
                    s.MarketplaceId,
                    s.Currency,
                    s.PeriodStart,
                    s.PeriodEnd,
                    HomeCurrency = m.Currency,
                    m.TimeZoneId,
                    Rate = dbContext.SettlementExchangeRates.FirstOrDefault(x => x.SettlementId == s.SettlementId && x.ToCurrency == m.Currency),
                })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (settlement is null)
        {
            return null;
        }

        var categories = await CategoryTotals(dbContext.SettlementLines.AsNoTracking().Where(l => l.SettlementId == settlementId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        // The same rule as rpt.vw_SettlementRate: no conversion in the marketplace's own currency,
        // otherwise the Operator's rate over the feed's.
        var same = settlement.Currency is null || string.Equals(settlement.Currency, settlement.HomeCurrency, StringComparison.OrdinalIgnoreCase);
        var rate = settlement.Rate;
        return new SettlementBooks(
            settlement.SettlementId,
            settlement.MarketplaceId,
            settlement.Currency,
            settlement.PeriodStart,
            settlement.PeriodEnd,
            [.. categories.Select(c => new SettlementCategoryTotal(c.AmountType, c.AmountDescription, c.Amount, c.LineCount))],
            settlement.HomeCurrency,
            same ? 1m : rate?.EffectiveRate,
            same || rate is null ? null
                : rate.ManualRate is not null ? $"entered by {rate.ManualBy} {rate.ManualAt:yyyy-MM-dd}"
                : $"{rate.MarketSource} {rate.MarketRateDate:yyyy-MM-dd}",
            same ? null : rate?.ManualHomeAmount,
            settlement.TimeZoneId);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SettlementCategoryTotal>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = await CategoryTotals(dbContext.SettlementLines.AsNoTracking())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. categories.Select(c => new SettlementCategoryTotal(c.AmountType, c.AmountDescription, c.Amount, c.LineCount))];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BookkeepingAccountMapping>> GetMappingsAsync(CancellationToken cancellationToken) =>
        await dbContext.BookkeepingAccountMappings.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public Task<string?> GetDepositAccountAsync(string marketplaceId, CancellationToken cancellationToken) =>
        dbContext.BookkeepingSettings.AsNoTracking()
            .Where(s => s.MarketplaceId == marketplaceId)
            .Select(s => (string?)s.DepositAccountName)
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BookkeepingExportSummary>> GetExportsAsync(string marketplaceId, int maxCount, CancellationToken cancellationToken) =>
        await dbContext.BookkeepingExports.AsNoTracking()
            .Where(e => e.MarketplaceId == marketplaceId)
            .OrderByDescending(e => e.GeneratedAt)
            .ThenByDescending(e => e.Id)
            .Take(maxCount)
            .Select(e => new BookkeepingExportSummary(e.Id, e.SettlementId, e.FileName, e.NetTotal, e.Currency, e.GeneratedAt, e.GeneratedBy))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public Task<BookkeepingExportFile?> GetExportFileAsync(string marketplaceId, long exportId, CancellationToken cancellationToken) =>
        dbContext.BookkeepingExports.AsNoTracking()
            .Where(e => e.Id == exportId && e.MarketplaceId == marketplaceId)
            .Select(e => new BookkeepingExportFile(e.FileName, e.Content))
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task SaveMappingsAsync(IReadOnlyCollection<AccountMappingInput> mappings, DateTimeOffset updatedAt, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mappings);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            var existing = await dbContext.BookkeepingAccountMappings
                .ToDictionaryAsync(m => (m.AmountType, m.AmountDescription), cancellationToken)
                .ConfigureAwait(false);

            foreach (var input in mappings)
            {
                var key = (input.AmountType, input.AmountDescription);
                var account = string.IsNullOrWhiteSpace(input.QuickBooksAccountName) ? null : input.QuickBooksAccountName;
                existing.TryGetValue(key, out var mapping);

                if (account is null)
                {
                    if (mapping is not null)
                    {
                        dbContext.BookkeepingAccountMappings.Remove(mapping);
                    }
                }
                else if (mapping is null)
                {
                    dbContext.BookkeepingAccountMappings.Add(new BookkeepingAccountMapping
                    {
                        AmountType = input.AmountType,
                        AmountDescription = input.AmountDescription,
                        QuickBooksAccountName = account,
                        UpdatedAt = updatedAt,
                        UpdatedBy = updatedBy,
                    });
                }
                else if (!string.Equals(mapping.QuickBooksAccountName, account, StringComparison.Ordinal))
                {
                    mapping.QuickBooksAccountName = account;
                    mapping.UpdatedAt = updatedAt;
                    mapping.UpdatedBy = updatedBy;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SaveDepositAccountAsync(string marketplaceId, string? depositAccountName, DateTimeOffset updatedAt, string updatedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);

        var settings = await dbContext.BookkeepingSettings.SingleOrDefaultAsync(s => s.MarketplaceId == marketplaceId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(depositAccountName))
        {
            if (settings is not null)
            {
                dbContext.BookkeepingSettings.Remove(settings);
            }
        }
        else if (settings is null)
        {
            dbContext.BookkeepingSettings.Add(new BookkeepingSettings
            {
                MarketplaceId = marketplaceId,
                DepositAccountName = depositAccountName,
                UpdatedAt = updatedAt,
                UpdatedBy = updatedBy,
            });
        }
        else if (!string.Equals(settings.DepositAccountName, depositAccountName, StringComparison.Ordinal))
        {
            settings.DepositAccountName = depositAccountName;
            settings.UpdatedAt = updatedAt;
            settings.UpdatedBy = updatedBy;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();
    }

    /// <inheritdoc/>
    public async Task<long> AddExportAsync(BookkeepingExport export, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(export);

        dbContext.BookkeepingExports.Add(export);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var id = export.Id;
        dbContext.ChangeTracker.Clear();
        return id;
    }

    /// <summary>
    /// Totals lines per (amount type, amount description). Amazon leaves the amount type blank on some rows
    /// (other transactions), so those fall back to the transaction type, and a missing description is empty.
    /// </summary>
    private static IQueryable<CategoryRow> CategoryTotals(IQueryable<SettlementLine> lines) =>
        lines
            .GroupBy(l => new
            {
                AmountType = l.AmountType != null && l.AmountType != "" ? l.AmountType : l.TransactionType,
                AmountDescription = l.AmountDescription ?? string.Empty,
            })
            .Select(g => new CategoryRow
            {
                AmountType = g.Key.AmountType,
                AmountDescription = g.Key.AmountDescription,
                Amount = g.Sum(l => l.Amount),
                LineCount = g.Count(),
            });

    private sealed class CategoryRow
    {
        public required string AmountType { get; init; }

        public required string AmountDescription { get; init; }

        public decimal Amount { get; init; }

        public int LineCount { get; init; }
    }
}
