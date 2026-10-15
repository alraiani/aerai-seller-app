using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Settlements;

namespace AERai.Web.Infrastructure.ExchangeRates;

/// <summary>
/// Fixed, plausible rates for tests and offline development, so settlements in another currency convert
/// without calling the internet. Rates are per unit in USD; other pairs go through USD.
/// </summary>
internal sealed class SimulatedExchangeRateProvider : IExchangeRateProvider
{
    /// <summary>The publisher stored with each rate.</summary>
    public const string Source = "Simulated";

    private static readonly Dictionary<string, decimal> UsdPerUnit = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 1m,
        ["CAD"] = 0.72m,
        ["MXN"] = 0.054m,
        ["GBP"] = 1.33m,
        ["EUR"] = 1.16m,
    };

    /// <inheritdoc/>
    public Task<ExchangeRateQuote?> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken)
    {
        if (!UsdPerUnit.TryGetValue(fromCurrency, out var from) || !UsdPerUnit.TryGetValue(toCurrency, out var to))
        {
            return Task.FromResult<ExchangeRateQuote?>(null);
        }

        return Task.FromResult<ExchangeRateQuote?>(new ExchangeRateQuote(Math.Round(from / to, 10), date, Source));
    }
}
