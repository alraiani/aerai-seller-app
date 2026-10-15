namespace AERai.Web.Application.Settlements;

/// <summary>A settlement paid in another currency than its marketplace's that has no market rate yet.</summary>
/// <param name="SettlementId">The settlement.</param>
/// <param name="FromCurrency">Currency it was paid in.</param>
/// <param name="ToCurrency">Its marketplace's currency.</param>
/// <param name="RateDate">The day its rate is taken for: the deposit date (UTC), else the period end.</param>
public sealed record SettlementRateNeed(string SettlementId, string FromCurrency, string ToCurrency, DateOnly RateDate);
