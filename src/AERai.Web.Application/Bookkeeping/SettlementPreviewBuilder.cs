using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Bookkeeping;

/// <summary>
/// Joins a settlement's totals with the user's account mapping. Pure, so it is unit-tested without a database.
/// </summary>
public static class SettlementPreviewBuilder
{
    /// <summary>Builds the preview for a settlement.</summary>
    /// <param name="settlement">The settlement's totals per kind of amount.</param>
    /// <param name="mappings">Every account mapping.</param>
    /// <param name="depositAccountName">The marketplace's deposit account, if set.</param>
    /// <returns>The preview, with rows ordered by amount type then description.</returns>
    public static SettlementPreview Build(SettlementBooks settlement, IReadOnlyCollection<BookkeepingAccountMapping> mappings, string? depositAccountName)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(mappings);

        var accounts = mappings.ToDictionary(
            m => (m.AmountType, m.AmountDescription),
            m => m.QuickBooksAccountName);

        var rows = settlement.Categories
            .Select(c => new SettlementPreviewRow(c.AmountType, c.AmountDescription, accounts.GetValueOrDefault((c.AmountType, c.AmountDescription)), c.Amount))
            .OrderBy(r => r.AmountType, StringComparer.Ordinal)
            .ThenBy(r => r.AmountDescription, StringComparer.Ordinal)
            .ToList();

        return new SettlementPreview(
            settlement.SettlementId,
            settlement.MarketplaceId,
            settlement.Currency,
            settlement.PeriodStart,
            settlement.PeriodEnd,
            rows.Sum(r => r.Amount),
            string.IsNullOrWhiteSpace(depositAccountName) ? null : depositAccountName,
            rows);
    }
}
