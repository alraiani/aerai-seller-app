namespace AERai.Web.Application.Bookkeeping;

/// <summary>The total of one kind of settlement amount.</summary>
/// <param name="AmountType">Amazon's amount type, or the row's transaction type when Amazon left it blank.</param>
/// <param name="AmountDescription">Amazon's amount description; empty when none.</param>
/// <param name="Amount">Signed total of every line of this kind, in the currency the settlement was paid in.</param>
/// <param name="LineCount">How many lines were summed.</param>
public sealed record SettlementCategoryTotal(string AmountType, string AmountDescription, decimal Amount, int LineCount);

/// <summary>A settlement reduced to what the books need: its period, its totals per kind of amount, and its exchange rate.</summary>
/// <param name="SettlementId">Amazon settlement identifier.</param>
/// <param name="MarketplaceId">Marketplace the payout settles.</param>
/// <param name="Currency">ISO 4217 currency it was paid in, when known.</param>
/// <param name="PeriodStart">Start of the settlement period.</param>
/// <param name="PeriodEnd">End of the settlement period.</param>
/// <param name="Categories">One total per (amount type, amount description).</param>
/// <param name="HomeCurrency">The marketplace's currency, which the books are kept in.</param>
/// <param name="ExchangeRate">One <paramref name="Currency"/> in <paramref name="HomeCurrency"/> (1 when they are the same), or <see langword="null"/> while unknown.</param>
/// <param name="RateNote">Where the rate came from, for the entry's memo (e.g. "ECB 2026-09-08", "entered by …"); <see langword="null"/> without conversion.</param>
/// <param name="ManualHomeAmount">The converted payout an Operator entered, which the entry's deposit line must equal exactly.</param>
/// <param name="TimeZoneId">The marketplace's time zone, whose local dates the entry and its pages use; <see langword="null"/> keeps UTC.</param>
public sealed record SettlementBooks(
    string SettlementId,
    string MarketplaceId,
    string? Currency,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    IReadOnlyList<SettlementCategoryTotal> Categories,
    string HomeCurrency,
    decimal? ExchangeRate,
    string? RateNote = null,
    decimal? ManualHomeAmount = null,
    string? TimeZoneId = null)
{
    /// <summary>Whether the settlement was paid in another currency than the books are kept in.</summary>
    public bool NeedsConversion => Currency is not null && !string.Equals(Currency, HomeCurrency, StringComparison.OrdinalIgnoreCase);
}
