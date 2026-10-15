using AERai.Web.Application.Common;

namespace AERai.Web.Application.Settlements;

/// <summary>
/// Exchange rates of settlements paid in another currency than their marketplace's (the CAD and MXN
/// payouts listed with the United States): fetched from the daily feed for the deposit date, and
/// adjustable by an Operator so the books match the bank exactly.
/// </summary>
public interface ISettlementExchangeRateService
{
    /// <summary>
    /// Fetches the market rate of every settlement that has none yet and whose deposit date has come.
    /// Settlements the feed cannot answer for are left for the next pass.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many got a rate and how many could not.</returns>
    Task<FillRatesResult> FillMissingAsync(CancellationToken cancellationToken);

    /// <summary>One settlement's rate details, for its page.</summary>
    /// <param name="marketplaceId">Marketplace whose settlements list holds it.</param>
    /// <param name="settlementId">The settlement.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The details, or <see langword="null"/> when the marketplace has no such settlement.</returns>
    Task<SettlementRateContext?> GetAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken);

    /// <summary>
    /// Overrides a settlement's rate with what the bank shows: either the converted amount that arrived
    /// (the rate is derived from it, and it becomes the converted payout exactly) or a rate.
    /// </summary>
    /// <param name="marketplaceId">Marketplace whose settlements list holds it.</param>
    /// <param name="settlementId">The settlement.</param>
    /// <param name="homeAmount">The amount received in the marketplace's currency, or <see langword="null"/> when giving a rate.</param>
    /// <param name="rate">One unit of the settlement's currency in the marketplace's, or <see langword="null"/> when giving an amount.</param>
    /// <param name="user">Email of the user making the change.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or why the override was refused.</returns>
    Task<Result> SetManualAsync(string marketplaceId, string settlementId, decimal? homeAmount, decimal? rate, string user, CancellationToken cancellationToken);

    /// <summary>Removes an Operator's override, so the market rate applies again.</summary>
    /// <param name="marketplaceId">Marketplace whose settlements list holds it.</param>
    /// <param name="settlementId">The settlement.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or why nothing changed.</returns>
    Task<Result> ResetAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken);
}
