namespace AERai.Web.Application.Bookkeeping;

/// <summary>One kind of settlement amount with the QuickBooks account it will post to.</summary>
/// <param name="AmountType">Amazon's amount type (or transaction type when blank).</param>
/// <param name="AmountDescription">Amazon's amount description; empty when none.</param>
/// <param name="QuickBooksAccountName">The mapped account, or <see langword="null"/> while unmapped.</param>
/// <param name="Amount">Signed total of the lines of this kind.</param>
public sealed record SettlementPreviewRow(string AmountType, string AmountDescription, string? QuickBooksAccountName, decimal Amount);

/// <summary>
/// A settlement as it would be exported: every kind of amount with its account, and what is still unmapped.
/// </summary>
/// <param name="SettlementId">Amazon settlement identifier.</param>
/// <param name="MarketplaceId">Marketplace the payout settles.</param>
/// <param name="Currency">ISO 4217 currency code, when known.</param>
/// <param name="PeriodStart">Start of the settlement period.</param>
/// <param name="PeriodEnd">End of the settlement period.</param>
/// <param name="NetTotal">Net payout: the sum of every line.</param>
/// <param name="DepositAccountName">The marketplace's deposit account, or <see langword="null"/> when not set.</param>
/// <param name="Rows">One row per kind of amount.</param>
public sealed record SettlementPreview(
    string SettlementId,
    string MarketplaceId,
    string? Currency,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    decimal NetTotal,
    string? DepositAccountName,
    IReadOnlyList<SettlementPreviewRow> Rows)
{
    /// <summary>Kinds of amount that have no account yet.</summary>
    public IReadOnlyList<SettlementPreviewRow> Unmapped { get; } = [.. Rows.Where(r => string.IsNullOrWhiteSpace(r.QuickBooksAccountName))];

    /// <summary>Whether any kind of amount still needs an account.</summary>
    public bool HasUnmapped => Unmapped.Count > 0;

    /// <summary>Whether the marketplace has a deposit account.</summary>
    public bool HasDepositAccount => !string.IsNullOrWhiteSpace(DepositAccountName);

    /// <summary>Whether the settlement can be exported now.</summary>
    public bool CanExport => Rows.Count > 0 && !HasUnmapped && HasDepositAccount;
}
