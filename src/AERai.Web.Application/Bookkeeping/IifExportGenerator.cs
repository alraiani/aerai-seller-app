using System.Globalization;
using System.Text;

namespace AERai.Web.Application.Bookkeeping;

/// <summary>
/// Builds a QuickBooks Desktop IIF general journal entry for one settlement: a TRNS line for the deposit
/// account (the net payout, matching the bank deposit) balanced by one SPL line per mapped account.
/// </summary>
/// <remarks>
/// Kinds of amount that share an account collapse into one summed SPL line. Each SPL is the negation of
/// that account's total, and the net payout is the sum of every settlement line, so the entry balances by
/// construction and needs no adjustment line. Pure and I/O-free so it is unit-tested in isolation. The
/// debit/credit sign convention follows the desktop app's export; test-import a generated file into a
/// QuickBooks sample company before relying on it for real books.
/// </remarks>
public static class IifExportGenerator
{
    /// <summary>The date format IIF expects.</summary>
    private const string DateFormat = "MM/dd/yyyy";

    /// <summary>Generates the IIF text.</summary>
    /// <param name="preview">The settlement to export; must be exportable.</param>
    /// <returns>The IIF file content, tab separated, one entry.</returns>
    /// <exception cref="InvalidOperationException">The settlement has unmapped amounts, no deposit account, or no lines.</exception>
    public static string Generate(SettlementPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (preview.HasUnmapped)
        {
            throw new InvalidOperationException(
                $"Settlement {preview.SettlementId} has {preview.Unmapped.Count} unmapped kind(s) of amount.");
        }

        if (!preview.HasDepositAccount)
        {
            throw new InvalidOperationException("The deposit account is not set.");
        }

        if (preview.Rows.Count == 0)
        {
            throw new InvalidOperationException($"Settlement {preview.SettlementId} has no lines.");
        }

        // The deposit lands after the period ends; the bank date is not part of the imported data, so the
        // period end is the date used.
        var date = (preview.PeriodEnd ?? preview.PeriodStart ?? DateTimeOffset.UtcNow).ToString(DateFormat, CultureInfo.InvariantCulture);
        var memo = $"Amazon settlement {preview.SettlementId}";

        var accountTotals = preview.Rows
            .GroupBy(r => r.QuickBooksAccountName!, StringComparer.Ordinal) // Non-null: unmapped rows were rejected above.
            .Select(g => (Account: g.Key, Amount: g.Sum(r => r.Amount)))
            .OrderBy(a => a.Account, StringComparer.Ordinal)
            .ToList();

        var builder = new StringBuilder();
        builder.Append("!TRNS\tTRNSID\tTRNSTYPE\tDATE\tACCNT\tNAME\tCLASS\tAMOUNT\tDOCNUM\tMEMO\r\n");
        builder.Append("!SPL\tSPLID\tTRNSTYPE\tDATE\tACCNT\tNAME\tCLASS\tAMOUNT\tDOCNUM\tMEMO\r\n");
        builder.Append("!ENDTRNS\r\n");
        builder.Append(Line("TRNS", 1, date, preview.DepositAccountName!, preview.NetTotal, preview.SettlementId, memo)); // Non-null: HasDepositAccount.

        var splitId = 1;
        foreach (var (account, amount) in accountTotals)
        {
            builder.Append(Line("SPL", splitId++, date, account, -amount, preview.SettlementId, memo));
        }

        builder.Append("ENDTRNS\r\n");
        return builder.ToString();
    }

    private static string Line(string recordType, int id, string date, string account, decimal amount, string docNumber, string memo) =>
        string.Join('\t',
            recordType, id.ToString(CultureInfo.InvariantCulture), "GENERAL JOURNAL", date, account, string.Empty, string.Empty,
            amount.ToString("0.00", CultureInfo.InvariantCulture), docNumber, memo) + "\r\n";
}
