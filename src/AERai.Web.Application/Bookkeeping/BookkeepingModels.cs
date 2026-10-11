namespace AERai.Web.Application.Bookkeeping;

/// <summary>An account typed against one kind of settlement amount on the mapping page.</summary>
/// <param name="AmountType">Amazon's amount type (or transaction type when blank).</param>
/// <param name="AmountDescription">Amazon's amount description; empty when none.</param>
/// <param name="QuickBooksAccountName">The account; blank removes the mapping.</param>
public sealed record AccountMappingInput(string AmountType, string AmountDescription, string? QuickBooksAccountName);

/// <summary>One kind of settlement amount seen in imported settlements, with its current account.</summary>
/// <param name="AmountType">Amazon's amount type (or transaction type when blank).</param>
/// <param name="AmountDescription">Amazon's amount description; empty when none.</param>
/// <param name="QuickBooksAccountName">The mapped account, or <see langword="null"/> while unmapped.</param>
/// <param name="LineCount">How many settlement lines of this kind have been imported.</param>
public sealed record AccountMappingRow(string AmountType, string AmountDescription, string? QuickBooksAccountName, int LineCount);

/// <summary>What the mapping page edits.</summary>
/// <param name="Rows">Every kind of amount seen, unmapped ones first.</param>
/// <param name="DepositAccountName">The current marketplace's deposit account, or <see langword="null"/> when not set.</param>
public sealed record AccountMappingView(IReadOnlyList<AccountMappingRow> Rows, string? DepositAccountName)
{
    /// <summary>How many kinds of amount have no account yet.</summary>
    public int UnmappedCount => Rows.Count(r => string.IsNullOrWhiteSpace(r.QuickBooksAccountName));
}

/// <summary>A generated export, without its file text.</summary>
/// <param name="Id">Export identifier.</param>
/// <param name="SettlementId">Settlement the file was generated for.</param>
/// <param name="FileName">Download name.</param>
/// <param name="NetTotal">Net payout in the file.</param>
/// <param name="Currency">ISO 4217 currency code, when known.</param>
/// <param name="GeneratedAt">When the file was generated.</param>
/// <param name="GeneratedBy">Email of the user who generated it.</param>
public sealed record BookkeepingExportSummary(long Id, string SettlementId, string FileName, decimal NetTotal, string? Currency, DateTimeOffset GeneratedAt, string GeneratedBy);

/// <summary>A generated export ready to download.</summary>
/// <param name="FileName">Download name.</param>
/// <param name="Content">The IIF text.</param>
public sealed record BookkeepingExportFile(string FileName, string Content);
