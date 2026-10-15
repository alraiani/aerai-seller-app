namespace AERai.Web.Infrastructure.ExchangeRates;

/// <summary>Where settlement exchange rates come from.</summary>
public enum ExchangeRateMode
{
    /// <summary>No feed: settlements in another currency wait for an Operator to enter their rate.</summary>
    Disabled = 0,

    /// <summary>Fixed plausible rates, for tests and offline development.</summary>
    Simulated = 1,

    /// <summary>The Frankfurter API (European Central Bank reference rates; free, no key).</summary>
    Live = 2,
}
