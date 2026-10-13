using AERai.Web.Application.Settlements;

namespace AERai.Web.Application.Abstractions;

/// <summary>Reads and writes <c>core.SettlementExchangeRate</c>, the rates of settlements paid in another currency.</summary>
public interface ISettlementExchangeRateRepository
{
    /// <summary>
    /// Settlements in another currency than their marketplace's that have no market rate yet and whose
    /// rate date has come (a deposit in the future has no published rate), oldest first.
    /// </summary>
    /// <param name="asOf">The latest rate date included.</param>
    /// <param name="max">At most this many.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The settlements that need a rate.</returns>
    Task<IReadOnlyList<SettlementRateNeed>> ListNeedingMarketRateAsync(DateOnly asOf, int max, CancellationToken cancellationToken);

    /// <summary>One settlement of a marketplace with its rate.</summary>
    /// <param name="marketplaceId">Marketplace whose settlements list holds it.</param>
    /// <param name="settlementId">The settlement.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The settlement, or <see langword="null"/> when the marketplace has no such settlement.</returns>
    Task<SettlementRateContext?> GetContextAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken);

    /// <summary>Stores the feed's rate for a settlement, keeping an Operator's override unless the target currency changed.</summary>
    /// <param name="need">The settlement and currencies.</param>
    /// <param name="quote">The feed's rate.</param>
    /// <param name="fetchedAt">When it was fetched.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SaveMarketRateAsync(SettlementRateNeed need, ExchangeRateQuote quote, DateTimeOffset fetchedAt, CancellationToken cancellationToken);

    /// <summary>Stores an Operator's override (creating the row when the feed never answered).</summary>
    /// <param name="settlementId">The settlement.</param>
    /// <param name="fromCurrency">Currency it was paid in.</param>
    /// <param name="toCurrency">Its marketplace's currency.</param>
    /// <param name="rate">The rate to use.</param>
    /// <param name="homeAmount">The converted payout entered, when the override was given as an amount.</param>
    /// <param name="by">Email of the user.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SaveManualAsync(string settlementId, string fromCurrency, string toCurrency, decimal rate, decimal? homeAmount, string by, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Removes an Operator's override, so the market rate applies again.</summary>
    /// <param name="settlementId">The settlement.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when saved.</returns>
    Task ClearManualAsync(string settlementId, CancellationToken cancellationToken);
}
