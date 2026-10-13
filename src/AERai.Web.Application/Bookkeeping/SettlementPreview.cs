namespace AERai.Web.Application.Bookkeeping;

/// <summary>One kind of settlement amount with the QuickBooks account it will post to.</summary>
/// <param name="AmountType">Amazon's amount type (or transaction type when blank).</param>
/// <param name="AmountDescription">Amazon's amount description; empty when none.</param>
/// <param name="QuickBooksAccountName">The mapped account, or <see langword="null"/> while unmapped.</param>
/// <param name="Amount">Signed total of the lines of this kind, in the currency the settlement was paid in.</param>
/// <param name="HomeAmount">The same converted into the books' currency (equal to <paramref name="Amount"/> without conversion), or <see langword="null"/> while no rate is known.</param>
public sealed record SettlementPreviewRow(string AmountType, string AmountDescription, string? QuickBooksAccountName, decimal Amount, decimal? HomeAmount);

/// <summary>One QuickBooks account's line in the journal entry, in the books' currency.</summary>
/// <param name="Account">The account.</param>
/// <param name="Amount">Signed total of the kinds of amount mapped to it.</param>
public sealed record SettlementAccountTotal(string Account, decimal Amount);

/// <summary>
/// A settlement as it would be exported: every kind of amount with its account, what is still unmapped,
/// and, for a settlement paid in another currency, its conversion into the books' currency.
/// </summary>
/// <param name="SettlementId">Amazon settlement identifier.</param>
/// <param name="MarketplaceId">Marketplace the payout settles.</param>
/// <param name="Currency">ISO 4217 currency of the entry: the books' currency (the settlement's own while it waits for a rate).</param>
/// <param name="PeriodStart">Start of the settlement period.</param>
/// <param name="PeriodEnd">End of the settlement period.</param>
/// <param name="NetTotal">Net payout in <paramref name="Currency"/>: the deposit line.</param>
/// <param name="DepositAccountName">The marketplace's deposit account, or <see langword="null"/> when not set.</param>
/// <param name="Rows">One row per kind of amount.</param>
/// <param name="OriginalCurrency">Currency the settlement was paid in, when the entry converts it; otherwise <see langword="null"/>.</param>
/// <param name="OriginalNetTotal">Net payout in <paramref name="OriginalCurrency"/>, when the entry converts it.</param>
/// <param name="ExchangeRate">The rate used, when the entry converts.</param>
/// <param name="RateNote">Where the rate came from, for the memo.</param>
/// <param name="NeedsRate">Whether the settlement is in another currency and has no rate yet, so it cannot be exported.</param>
/// <param name="AccountTotals">The entry's split lines (empty while unmapped or waiting for a rate).</param>
public sealed record SettlementPreview(
    string SettlementId,
    string MarketplaceId,
    string? Currency,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    decimal NetTotal,
    string? DepositAccountName,
    IReadOnlyList<SettlementPreviewRow> Rows,
    string? OriginalCurrency = null,
    decimal? OriginalNetTotal = null,
    decimal? ExchangeRate = null,
    string? RateNote = null,
    bool NeedsRate = false,
    IReadOnlyList<SettlementAccountTotal>? AccountTotals = null)
{
    /// <summary>Kinds of amount that have no account yet.</summary>
    public IReadOnlyList<SettlementPreviewRow> Unmapped { get; } = [.. Rows.Where(r => string.IsNullOrWhiteSpace(r.QuickBooksAccountName))];

    /// <summary>Whether any kind of amount still needs an account.</summary>
    public bool HasUnmapped => Unmapped.Count > 0;

    /// <summary>Whether the marketplace has a deposit account.</summary>
    public bool HasDepositAccount => !string.IsNullOrWhiteSpace(DepositAccountName);

    /// <summary>Whether the entry converts the settlement from another currency.</summary>
    public bool IsConverted => OriginalCurrency is not null;

    /// <summary>
    /// What converting each account separately left over against the converted payout, already added to
    /// the largest account line so the entry balances (a cent or two; zero without conversion).
    /// </summary>
    public decimal RoundingAdjustment { get; init; }

    /// <summary>Whether the settlement can be exported now.</summary>
    public bool CanExport => Rows.Count > 0 && !HasUnmapped && HasDepositAccount && !NeedsRate;
}
