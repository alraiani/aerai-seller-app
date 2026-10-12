namespace AERai.Web.Application.Bookkeeping;

/// <summary>The total of one kind of settlement amount.</summary>
/// <param name="AmountType">Amazon's amount type, or the row's transaction type when Amazon left it blank.</param>
/// <param name="AmountDescription">Amazon's amount description; empty when none.</param>
/// <param name="Amount">Signed total of every line of this kind.</param>
/// <param name="LineCount">How many lines were summed.</param>
public sealed record SettlementCategoryTotal(string AmountType, string AmountDescription, decimal Amount, int LineCount);

/// <summary>A settlement reduced to what the books need: its period and its totals per kind of amount.</summary>
/// <param name="SettlementId">Amazon settlement identifier.</param>
/// <param name="MarketplaceId">Marketplace the payout settles.</param>
/// <param name="Currency">ISO 4217 currency code, when known.</param>
/// <param name="PeriodStart">Start of the settlement period.</param>
/// <param name="PeriodEnd">End of the settlement period.</param>
/// <param name="Categories">One total per (amount type, amount description).</param>
public sealed record SettlementBooks(
    string SettlementId,
    string MarketplaceId,
    string? Currency,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    IReadOnlyList<SettlementCategoryTotal> Categories);
