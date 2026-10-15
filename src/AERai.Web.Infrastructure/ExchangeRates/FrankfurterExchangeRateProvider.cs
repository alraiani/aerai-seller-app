using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Settlements;

namespace AERai.Web.Infrastructure.ExchangeRates;

/// <summary>
/// Reads daily reference rates from the Frankfurter API, which republishes the European Central Bank's
/// rates (one per business day, about 4 PM CET; free and without a key). Not an Amazon API, so it has its
/// own client and never goes through the SP-API pipeline.
/// </summary>
/// <param name="http">Client whose base address is <see cref="ExchangeRateOptions.BaseUrl"/>.</param>
internal sealed class FrankfurterExchangeRateProvider(HttpClient http) : IExchangeRateProvider
{
    /// <summary>The publisher stored with each rate.</summary>
    public const string Source = "ECB";

    /// <inheritdoc/>
    public async Task<ExchangeRateQuote?> GetRateAsync(string fromCurrency, string toCurrency, DateOnly date, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(toCurrency);

        // A date on a weekend or holiday answers with the business day before, and the response says which day it is.
        var path = string.Create(CultureInfo.InvariantCulture,
            $"{date:yyyy-MM-dd}?base={Uri.EscapeDataString(fromCurrency)}&symbols={Uri.EscapeDataString(toCurrency)}");
        using var response = await http.GetAsync(new Uri(path, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
        {
            // An unknown currency (the ECB doesn't publish every one).
            return null;
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RatesResponse>(cancellationToken).ConfigureAwait(false);
        if (body?.Rates is null || !body.Rates.TryGetValue(toCurrency.ToUpperInvariant(), out var rate) || rate <= 0 || body.Date is not { } rateDate)
        {
            return null;
        }

        return new ExchangeRateQuote(rate, rateDate, Source);
    }

    private sealed record RatesResponse(DateOnly? Date, Dictionary<string, decimal>? Rates);
}
