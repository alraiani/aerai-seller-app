using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Settlements;

/// <summary>Default <see cref="ISettlementExchangeRateService"/>.</summary>
/// <param name="repository">Settlement exchange rate persistence.</param>
/// <param name="provider">The daily rates feed.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class SettlementExchangeRateService(
    ISettlementExchangeRateRepository repository,
    IExchangeRateProvider provider,
    TimeProvider clock,
    ILogger<SettlementExchangeRateService> logger) : ISettlementExchangeRateService
{
    /// <summary>Most settlements given a rate in one pass; a region pays out a few a week, so this only bounds a first run.</summary>
    public const int MaxPerFill = 200;

    /// <summary>The largest rate accepted from an Operator, to catch a rate typed the wrong way round (MXN→USD is about 0.05, not 18).</summary>
    public const decimal MaxManualRate = 1000m;

    /// <summary>Decimal places a rate is kept to (the column is decimal(19,10)).</summary>
    private const int RateDecimals = 10;

    /// <inheritdoc/>
    public async Task<FillRatesResult> FillMissingAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var needs = await repository.ListNeedingMarketRateAsync(DateOnly.FromDateTime(now.UtcDateTime), MaxPerFill, cancellationToken).ConfigureAwait(false);
        if (needs.Count == 0)
        {
            return new FillRatesResult(0, 0);
        }

        // Settlements paid out on the same day in the same currency share one request.
        var quotes = new Dictionary<(string From, string To, DateOnly Date), ExchangeRateQuote?>();
        var filled = 0;
        var unavailable = 0;
        foreach (var need in needs)
        {
            var key = (need.FromCurrency.ToUpperInvariant(), need.ToCurrency.ToUpperInvariant(), need.RateDate);
            if (!quotes.TryGetValue(key, out var quote))
            {
                quote = await TryGetRateAsync(key.Item1, key.Item2, key.RateDate, cancellationToken).ConfigureAwait(false);
                quotes[key] = quote;
            }

            if (quote is null)
            {
                unavailable++;
                continue;
            }

            await repository.SaveMarketRateAsync(need, quote, now, cancellationToken).ConfigureAwait(false);
            filled++;
        }

        LogFilled(filled, unavailable);
        return new FillRatesResult(filled, unavailable);
    }

    /// <inheritdoc/>
    public Task<SettlementRateContext?> GetAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(settlementId);
        return repository.GetContextAsync(marketplaceId, settlementId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result> SetManualAsync(string marketplaceId, string settlementId, decimal? homeAmount, decimal? rate, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(settlementId);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        if (homeAmount is null == rate is null)
        {
            return Result.Failure("Enter either the amount received or a rate, not both.");
        }

        var settlement = await repository.GetContextAsync(marketplaceId, settlementId, cancellationToken).ConfigureAwait(false);
        if (settlement is null)
        {
            return Result.Failure("Settlement not found.");
        }

        if (!settlement.NeedsConversion)
        {
            return Result.Failure($"This settlement is paid in {settlement.HomeCurrency}; it has nothing to convert.");
        }

        decimal effective;
        if (homeAmount is { } received)
        {
            // The rate is derived from the payout, so a payout of zero (a settlement that only moved
            // reserves) can only be given a rate, and the received amount must have the payout's sign.
            if (settlement.Net == 0)
            {
                return Result.Failure("The payout is zero, so the amount received can't set a rate; enter the rate instead.");
            }

            if (Math.Sign(received) != Math.Sign(settlement.Net))
            {
                return Result.Failure(settlement.Net > 0
                    ? "The payout is positive, so the amount received must be too."
                    : "The payout is negative (Amazon charged it), so the amount must be negative too.");
            }

            effective = Math.Round(received / settlement.Net, RateDecimals, MidpointRounding.AwayFromZero);
        }
        else
        {
            effective = Math.Round(rate!.Value, RateDecimals, MidpointRounding.AwayFromZero); // Non-null: exactly one of the two was given.
            if (effective <= 0 || effective > MaxManualRate)
            {
                return Result.Failure($"Enter a rate above 0 and up to {MaxManualRate:N0} (one {settlement.Currency} in {settlement.HomeCurrency}).");
            }
        }

        await repository.SaveManualAsync(
            settlementId, settlement.Currency!, settlement.HomeCurrency, effective, homeAmount is { } amount ? Math.Round(amount, 2) : null, user, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> ResetAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(settlementId);

        var settlement = await repository.GetContextAsync(marketplaceId, settlementId, cancellationToken).ConfigureAwait(false);
        if (settlement is null)
        {
            return Result.Failure("Settlement not found.");
        }

        if (settlement.Rate?.ManualRate is null)
        {
            return Result.Failure("This settlement has no adjusted rate.");
        }

        await repository.ClearManualAsync(settlementId, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>Asks the feed, treating an unreachable feed like a missing rate so the next pass retries.</summary>
    private async Task<ExchangeRateQuote?> TryGetRateAsync(string from, string to, DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            return await provider.GetRateAsync(from, to, date, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // A timeout surfaces as a cancellation that nobody asked for.
            LogFeedFailed(ex, from, to, date);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Settlement exchange rates: {Filled} filled, {Unavailable} unavailable")]
    private partial void LogFilled(int filled, int unavailable);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The exchange rates feed failed for {FromCurrency}->{ToCurrency} on {Date}; the next pass retries")]
    private partial void LogFeedFailed(Exception exception, string fromCurrency, string toCurrency, DateOnly date);
}
