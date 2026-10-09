using System.Globalization;
using AERai.Seller.Domain.Staging;

namespace AERai.Seller.Application.Reports;

/// <summary>
/// Maps parsed GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE_V2 ("long" layout) rows into a SettlementReport header (one per
/// distinct settlement-id — the settlement-level columns are repeated on every data row) plus that
/// settlement's SettlementLineItems (one per detail row; summary rows with no amount-type are
/// skipped). Column names below match the _V2 header confirmed by a live SP-API pull on 2026-10-05.
///
/// The non-_V2 GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE is a different, "wide" layout with no
/// amount-type/amount columns; feeding it here would skip every detail row and silently import zero
/// line items, so Parse throws instead when those columns are absent from a non-empty report.
/// </summary>
public static class SettlementReportParser
{
    private static readonly string[] RequiredDetailColumns = ["amount-type", "amount"];

    public static (IReadOnlyList<SettlementReport> Settlements, IReadOnlyList<SettlementLineItem> LineItems) Parse(
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows, DateTimeOffset syncedAt)
    {
        // TsvReportParser drops trailing columns a short row doesn't reach, so a column missing from
        // one row isn't proof it's missing from the header — only missing from every row is.
        var missingColumns = RequiredDetailColumns
            .Where(column => rows.Count > 0 && !rows.Any(row => row.ContainsKey(column)))
            .ToList();
        if (missingColumns.Count > 0)
        {
            throw new FormatException(
                $"Settlement report is missing required column(s) {string.Join(", ", missingColumns)}. " +
                "Expected the GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE_V2 (long) layout; the non-_V2 " +
                "GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE (wide) layout is not supported.");
        }

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
