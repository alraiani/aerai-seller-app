using AERai.Web.Application.Settlements;

namespace AERai.Web.Application.Abstractions;

/// <summary>A daily exchange rates feed.</summary>
public interface IExchangeRateProvider
{
    /// <summary>The rate for one day: one unit of <paramref name="fromCurrency"/> in <paramref name="toCurrency"/>.</summary>
    /// <param name="fromCurrency">ISO 4217 currency to convert from.</param>
    /// <param name="toCurrency">ISO 4217 currency to convert into.</param>
    /// <param name="date">The day wanted; on a weekend or holiday the feed answers with the business day before.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The quote, or <see langword="null"/> when the feed has no rate for the pair (or is turned off).</returns>
    /// <exception cref="HttpRequestException">The feed could not be reached.</exception>
    Task<ExchangeRateQuote?> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken);
}
