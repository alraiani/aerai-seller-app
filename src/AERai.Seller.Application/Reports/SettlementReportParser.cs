using System.Globalization;
using AERai.Seller.Domain.Staging;

namespace AERai.Seller.Application.Reports;

/// <summary>
/// Maps parsed GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE rows into a SettlementReport header (one per
/// distinct settlement-id — the settlement-level columns are repeated on every data row) plus that
/// settlement's SettlementLineItems (one per detail row; summary rows with no amount-type are
/// skipped). Column names below are Amazon's documented flat-file header names — unlike
/// InventoryPlanningReportParser's, they have NOT yet been confirmed against a real report pull.
/// Verify them against an actual settlement export during the first real sync and adjust here if
/// Amazon's live output differs.
/// </summary>
public static class SettlementReportParser
{
    public static (IReadOnlyList<SettlementReport> Settlements, IReadOnlyList<SettlementLineItem> LineItems) Parse(
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows, DateTimeOffset syncedAt)
    {
        var settlementsById = new Dictionary<string, SettlementReport>(StringComparer.Ordinal);
        var lineItems = new List<SettlementLineItem>();

        foreach (var row in rows)
        {
            if (!row.TryGetValue("settlement-id", out var settlementId) || string.IsNullOrWhiteSpace(settlementId))
            {
                continue;
            }

            if (!settlementsById.ContainsKey(settlementId))
            {
                settlementsById[settlementId] = new SettlementReport
                {
                    SettlementId = settlementId,
                    MarketplaceId = row.GetValueOrDefault("marketplace-name") ?? "UNKNOWN",
                    FinancialEventGroupStart = ParseDate(row.GetValueOrDefault("settlement-start-date")) ?? default,
                    FinancialEventGroupEnd = ParseDate(row.GetValueOrDefault("settlement-end-date")) ?? default,
                    TotalAmount = ParseAmount(row.GetValueOrDefault("total-amount")) ?? 0m,
                    Currency = row.GetValueOrDefault("currency") ?? "USD",
                    DepositDate = ParseDate(row.GetValueOrDefault("deposit-date")),
                };
            }

            if (!row.TryGetValue("amount-type", out var amountType) || string.IsNullOrWhiteSpace(amountType))
            {
                continue;
            }

            var amount = ParseAmount(row.GetValueOrDefault("amount"));
            if (amount is null)
            {
                continue;
            }

            lineItems.Add(new SettlementLineItem
            {
                SettlementId = settlementId,
                AmazonOrderId = NullIfEmpty(row.GetValueOrDefault("order-id")),
                Sku = NullIfEmpty(row.GetValueOrDefault("sku")),
                AmountType = amountType,
                AmountDescription = NullIfEmpty(row.GetValueOrDefault("amount-description")) ?? amountType,
                Amount = amount.Value,
                Currency = row.GetValueOrDefault("currency") ?? "USD",
                PostedDate = ParseDate(row.GetValueOrDefault("posted-date")) ?? syncedAt,
            });
        }

        return (settlementsById.Values.ToList(), lineItems);
    }

    private static DateTimeOffset? ParseDate(string? raw)
        => !string.IsNullOrWhiteSpace(raw)
            && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value
            : null;

    private static decimal? ParseAmount(string? raw)
        => !string.IsNullOrWhiteSpace(raw)
            && decimal.TryParse(raw, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
