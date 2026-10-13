namespace AERai.Web.Application.Settlements;

/// <summary>What a pass over settlements missing an exchange rate did.</summary>
/// <param name="Filled">Settlements that got a market rate.</param>
/// <param name="Unavailable">Settlements the feed had no rate for, or could not be asked about.</param>
public sealed record FillRatesResult(int Filled, int Unavailable);
