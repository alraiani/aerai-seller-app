using AERai.Seller.Domain;
using AERai.Seller.Domain.Staging;

namespace AERai.Seller.Application.Bookkeeping;

public sealed record SettlementSummaryRow(
    string AmountType,
    string AmountDescription,
    string? QuickBooksAccountName,
    decimal Amount);

public sealed record SettlementSummary(
    string SettlementId,
    string Currency,
    decimal NetTotal,
    IReadOnlyList<SettlementSummaryRow> Rows,
    IReadOnlyList<(string AmountType, string AmountDescription)> UnmappedCategories)
{
    public bool HasUnmappedCategories => UnmappedCategories.Count > 0;
}

/// <summary>
/// Groups a settlement's raw line items by (AmountType, AmountDescription), joins each group against
/// the user's account mapping, and reports which categories are still unmapped. Pure — no DB/HTTP —
/// so it's unit-testable in isolation.
/// </summary>
public static class SettlementSummaryBuilder
{
    public static SettlementSummary Build(
        SettlementReport settlement,
        IReadOnlyList<SettlementLineItem> lineItems,
        IReadOnlyList<BookkeepingAccountMapping> mappings)
    {
        var mappingLookup = mappings.ToDictionary(
            m => (m.AmountType, m.AmountDescription),
            m => m.QuickBooksAccountName);

        var rows = lineItems
            .GroupBy(li => (li.AmountType, li.AmountDescription))
            .Select(g =>
            {
                var accountName = mappingLookup.GetValueOrDefault(g.Key);
                return new SettlementSummaryRow(g.Key.AmountType, g.Key.AmountDescription, accountName, g.Sum(li => li.Amount));
            })
            .OrderBy(r => r.AmountType, StringComparer.Ordinal)
            .ThenBy(r => r.AmountDescription, StringComparer.Ordinal)
            .ToList();

        var unmapped = rows
            .Where(r => string.IsNullOrWhiteSpace(r.QuickBooksAccountName))
            .Select(r => (r.AmountType, r.AmountDescription))
            .ToList();

        return new SettlementSummary(settlement.SettlementId, settlement.Currency, settlement.TotalAmount, rows, unmapped);
    }
}
