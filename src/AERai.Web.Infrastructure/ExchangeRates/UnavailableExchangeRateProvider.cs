using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Settlements;

namespace AERai.Web.Infrastructure.ExchangeRates;

/// <summary>The feed when <see cref="ExchangeRateMode.Disabled"/>: never has a rate, so Operators enter them.</summary>
internal sealed class UnavailableExchangeRateProvider : IExchangeRateProvider
{
    /// <inheritdoc/>
    public Task<ExchangeRateQuote?> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken) =>
        Task.FromResult<ExchangeRateQuote?>(null);
}
