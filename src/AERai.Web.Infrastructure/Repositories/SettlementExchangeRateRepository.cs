using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="ISettlementExchangeRateRepository"/>.</summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class SettlementExchangeRateRepository(AppDbContext dbContext) : ISettlementExchangeRateRepository
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<SettlementRateNeed>> ListNeedingMarketRateAsync(DateOnly asOf, int max, CancellationToken cancellationToken)
    {
        var candidates = await (
                from s in dbContext.Settlements.AsNoTracking()
                join m in dbContext.Marketplaces.AsNoTracking() on s.MarketplaceId equals m.MarketplaceId
                where s.Currency != null && s.Currency != m.Currency
                    && !dbContext.SettlementExchangeRates.Any(x => x.SettlementId == s.SettlementId && x.ToCurrency == m.Currency && x.MarketRate != null)
                select new { s.SettlementId, s.Currency, HomeCurrency = m.Currency, RateAt = s.DepositDate ?? s.PeriodEnd })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Few settlements are ever in another currency, so the date test (a UTC day) is done here.
        return candidates
            .Where(c => c.RateAt is not null)
            .Select(c => new SettlementRateNeed(c.SettlementId, c.Currency!, c.HomeCurrency, DateOnly.FromDateTime(c.RateAt!.Value.UtcDateTime)))
            .Where(n => n.RateDate <= asOf)
            .OrderBy(n => n.RateDate)
            .ThenBy(n => n.SettlementId, StringComparer.Ordinal)
            .Take(max)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<SettlementRateContext?> GetContextAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken)
    {
        var settlement = await (
                from s in dbContext.Settlements.AsNoTracking()
                join m in dbContext.Marketplaces.AsNoTracking() on s.MarketplaceId equals m.MarketplaceId
                where s.MarketplaceId == marketplaceId && s.SettlementId == settlementId
                select new
                {
                    s.SettlementId,
                    s.Currency,
                    HomeCurrency = m.Currency,
                    RateAt = s.DepositDate ?? s.PeriodEnd,
                    Net = dbContext.SettlementLines.Where(l => l.SettlementId == s.SettlementId).Sum(l => (decimal?)l.Amount) ?? 0m,
                    Rate = dbContext.SettlementExchangeRates.FirstOrDefault(x => x.SettlementId == s.SettlementId && x.ToCurrency == m.Currency),
                    IsExported = dbContext.BookkeepingExports.Any(e => e.SettlementId == s.SettlementId),
                })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return settlement is null
            ? null
            : new SettlementRateContext(
                settlement.SettlementId,
                settlement.Currency,
                settlement.HomeCurrency,
                settlement.Net,
                settlement.RateAt is { } at ? DateOnly.FromDateTime(at.UtcDateTime) : null,
                settlement.Rate,
                settlement.IsExported);
    }

    /// <inheritdoc/>
    public async Task SaveMarketRateAsync(SettlementRateNeed need, ExchangeRateQuote quote, DateTimeOffset fetchedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(need);
        ArgumentNullException.ThrowIfNull(quote);

        var row = await GetOrAddAsync(need.SettlementId, need.FromCurrency, need.ToCurrency, cancellationToken).ConfigureAwait(false);
        row.MarketRate = quote.Rate;
        row.MarketRateDate = quote.Date;
        row.MarketSource = quote.Source;
        row.MarketFetchedAt = fetchedAt;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SaveManualAsync(string settlementId, string fromCurrency, string toCurrency, decimal rate, decimal? homeAmount, string by, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var row = await GetOrAddAsync(settlementId, fromCurrency, toCurrency, cancellationToken).ConfigureAwait(false);
        row.ManualRate = rate;
        row.ManualHomeAmount = homeAmount;
        row.ManualBy = by;
        row.ManualAt = at;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task ClearManualAsync(string settlementId, CancellationToken cancellationToken) =>
        dbContext.SettlementExchangeRates
            .Where(x => x.SettlementId == settlementId)
            .ExecuteUpdateAsync(
                u => u.SetProperty(x => x.ManualRate, (decimal?)null)
                    .SetProperty(x => x.ManualHomeAmount, (decimal?)null)
                    .SetProperty(x => x.ManualBy, (string?)null)
                    .SetProperty(x => x.ManualAt, (DateTimeOffset?)null),
                cancellationToken);

    /// <summary>
    /// The settlement's rate row, tracked; a new one when it has none. A row made for another pair (the
    /// settlement moved to a marketplace with another currency) no longer applies, so it starts over.
    /// </summary>
    private async Task<SettlementExchangeRate> GetOrAddAsync(string settlementId, string fromCurrency, string toCurrency, CancellationToken cancellationToken)
    {
        var row = await dbContext.SettlementExchangeRates.FirstOrDefaultAsync(x => x.SettlementId == settlementId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            row = new SettlementExchangeRate { SettlementId = settlementId, FromCurrency = fromCurrency, ToCurrency = toCurrency };
            dbContext.SettlementExchangeRates.Add(row);
        }
        else if (!string.Equals(row.FromCurrency, fromCurrency, StringComparison.OrdinalIgnoreCase) || !string.Equals(row.ToCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
        {
            row.FromCurrency = fromCurrency;
            row.ToCurrency = toCurrency;
            row.MarketRate = null;
            row.MarketRateDate = null;
            row.MarketSource = null;
            row.MarketFetchedAt = null;
            row.ManualRate = null;
            row.ManualHomeAmount = null;
            row.ManualBy = null;
            row.ManualAt = null;
        }

        return row;
    }
}
