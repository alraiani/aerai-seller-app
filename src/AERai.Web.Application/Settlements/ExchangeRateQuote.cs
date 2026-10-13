namespace AERai.Web.Application.Settlements;

/// <summary>One rate from the daily feed.</summary>
/// <param name="Rate">One unit of the source currency in the target currency.</param>
/// <param name="Date">The business day the rate is for (on or before the day asked for).</param>
/// <param name="Source">Who publishes it, e.g. <c>ECB</c>.</param>
public sealed record ExchangeRateQuote(decimal Rate, DateOnly Date, string Source);
