using System.Globalization;
using System.Text;
using AERai.Seller.Domain.Staging;

namespace AERai.Seller.Application.Bookkeeping;

/// <summary>
/// Builds an IIF General Journal Entry: one TRNS line for the deposit account (amount =
/// settlement.NetTotal, matching the exact bank deposit) balanced by one SPL line per mapped
/// QuickBooks account (categories sharing an account collapse into a single summed line). Amazon's
/// own settlement line items already net to the deposit total (that's how Amazon computes the
/// payout), so each SPL amount is simply the negation of that account's summed raw amount — the
/// entry balances by construction. If the raw line items don't sum exactly to NetTotal (rounding or
/// an undisclosed adjustment), a single labeled "Unclassified settlement adjustment" line absorbs the
/// difference so the file always balances exactly; that residual should normally be at or near zero.
///
/// Pure/no I/O so it's unit-testable in isolation. Debit/credit sign conventions here are a best
/// effort without a live QuickBooks connection — per docs/plan.md, a real generated file must be
/// test-imported into a QuickBooks Desktop sample company file before this is relied on.
/// </summary>
public static class IifExportGenerator
{
    private const decimal ReconciliationTolerance = 0.01m;

    public static string Generate(SettlementSummary summary, SettlementReport settlement, string depositAccountName)
    {
        if (summary.HasUnmappedCategories)
        {
            throw new InvalidOperationException(
                $"Cannot generate IIF export for settlement {summary.SettlementId}: " +
                $"{summary.UnmappedCategories.Count} unmapped categor{(summary.UnmappedCategories.Count == 1 ? "y" : "ies")}.");
        }

        if (string.IsNullOrWhiteSpace(depositAccountName))
        {
            throw new InvalidOperationException("Deposit account is not configured — set it on the Account Mapping page first.");
        }

        var date = (settlement.DepositDate ?? settlement.FinancialEventGroupEnd).ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
        var memo = $"Amazon settlement {summary.SettlementId}";

        var accountTotals = summary.Rows
            .GroupBy(r => r.QuickBooksAccountName!)
            .Select(g => (Account: g.Key, Amount: g.Sum(r => r.Amount)))
            .OrderBy(a => a.Account, StringComparer.Ordinal)
            .ToList();

        var reconciliationDifference = summary.NetTotal - accountTotals.Sum(a => a.Amount);

        var sb = new StringBuilder();
        sb.AppendLine("!TRNS\tTRNSID\tTRNSTYPE\tDATE\tACCNT\tNAME\tCLASS\tAMOUNT\tDOCNUM\tMEMO");
        sb.AppendLine("!SPL\tSPLID\tTRNSTYPE\tDATE\tACCNT\tNAME\tCLASS\tAMOUNT\tDOCNUM\tMEMO");
        sb.AppendLine("!ENDTRNS");

        sb.AppendLine(FormatLine("TRNS", 1, date, depositAccountName, summary.NetTotal, summary.SettlementId, memo));

        var splId = 1;
        foreach (var (account, amount) in accountTotals)
        {
            sb.AppendLine(FormatLine("SPL", splId++, date, account, -amount, summary.SettlementId, memo));
        }

        if (Math.Abs(reconciliationDifference) >= ReconciliationTolerance)
        {
            sb.AppendLine(FormatLine(
                "SPL", splId, date, depositAccountName, -reconciliationDifference, summary.SettlementId,
                "Unclassified settlement adjustment"));
        }

        sb.AppendLine("ENDTRNS");
        return sb.ToString();
    }

    private static string FormatLine(string recordType, int id, string date, string account, decimal amount, string docNum, string memo)
        => string.Join('\t',
            recordType, id, "GENERAL JOURNAL", date, account, string.Empty, string.Empty,
            amount.ToString("0.00", CultureInfo.InvariantCulture), docNum, memo);
}
