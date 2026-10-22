using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Bookkeeping;

/// <summary>
/// Joins a settlement's totals with the user's account mapping and, for a settlement paid in another
/// currency, converts it into the books' currency. Pure, so it is unit-tested without a database.
/// </summary>
/// <remarks>
/// A converted entry must still balance and its deposit line must match the bank: the deposit line is
/// the converted payout (exactly the amount an Operator entered, when they gave one), each account is
/// its total converted and rounded to the cent, and the few cents those roundings leave over are added
/// to the largest account line (shown on the preview as the rounding adjustment).
/// </remarks>
public static class SettlementPreviewBuilder
{
    /// <summary>Builds the preview for a settlement.</summary>
    /// <param name="settlement">The settlement's totals per kind of amount, and its rate.</param>
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

        // The period is expressed in the marketplace's local time, so the entry is dated the day Seller
        // Central shows (Amazon closes periods at an hour that is already the next day in UTC).
        var zone = settlement.TimeZoneId is { } zoneId ? TimeZoneInfo.FindSystemTimeZoneById(zoneId) : TimeZoneInfo.Utc;
        settlement = settlement with
        {
            PeriodStart = settlement.PeriodStart is { } start ? TimeZoneInfo.ConvertTime(start, zone) : null,
            PeriodEnd = settlement.PeriodEnd is { } end ? TimeZoneInfo.ConvertTime(end, zone) : null,
        };

        var converts = settlement.NeedsConversion;
        var rate = converts ? settlement.ExchangeRate : 1m;
        var rows = settlement.Categories
            .Select(c => new SettlementPreviewRow(
                c.AmountType,
                c.AmountDescription,
                accounts.GetValueOrDefault((c.AmountType, c.AmountDescription)),
                c.Amount,
                rate is { } r ? Convert(c.Amount, r) : null))
            .OrderBy(r => r.AmountType, StringComparer.Ordinal)
            .ThenBy(r => r.AmountDescription, StringComparer.Ordinal)
            .ToList();
        var deposit = string.IsNullOrWhiteSpace(depositAccountName) ? null : depositAccountName;
        var originalNet = rows.Sum(r => r.Amount);

        if (!converts)
        {
            return new SettlementPreview(
                settlement.SettlementId, settlement.MarketplaceId, settlement.Currency, settlement.PeriodStart, settlement.PeriodEnd,
                originalNet, deposit, rows, AccountTotals: AccountTotalsOf(rows, 1m, originalNet).Totals);
        }

        if (rate is not { } exchangeRate)
        {
            // Shown as paid until a rate is known; it cannot be exported meanwhile.
            return new SettlementPreview(
                settlement.SettlementId, settlement.MarketplaceId, settlement.Currency, settlement.PeriodStart, settlement.PeriodEnd,
                originalNet, deposit, rows, NeedsRate: true, AccountTotals: []);
        }

        var net = settlement.ManualHomeAmount ?? Convert(originalNet, exchangeRate);
        var (totals, adjustment) = AccountTotalsOf(rows, exchangeRate, net);
        return new SettlementPreview(
            settlement.SettlementId, settlement.MarketplaceId, settlement.HomeCurrency, settlement.PeriodStart, settlement.PeriodEnd,
            net, deposit, rows, settlement.Currency, originalNet, exchangeRate, settlement.RateNote, NeedsRate: false, totals)
        {
            RoundingAdjustment = adjustment,
        };
    }

    /// <summary>An amount converted and rounded to the cent.</summary>
    private static decimal Convert(decimal amount, decimal rate) => Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Each mapped account's converted total, with what the roundings leave against <paramref name="net"/>
    /// added to the largest one; empty while any kind of amount is unmapped.
    /// </summary>
    private static (IReadOnlyList<SettlementAccountTotal> Totals, decimal Adjustment) AccountTotalsOf(List<SettlementPreviewRow> rows, decimal rate, decimal net)
    {
        if (rows.Count == 0 || rows.Any(r => string.IsNullOrWhiteSpace(r.QuickBooksAccountName)))
        {
            return ([], 0m);
        }

        var totals = rows
            .GroupBy(r => r.QuickBooksAccountName!, StringComparer.Ordinal) // Non-null: unmapped rows returned above.
            .Select(g => new SettlementAccountTotal(g.Key, Convert(g.Sum(r => r.Amount), rate)))
            .OrderBy(a => a.Account, StringComparer.Ordinal)
            .ToList();

        var adjustment = net - totals.Sum(t => t.Amount);
        if (adjustment != 0)
        {
            var largest = totals.OrderByDescending(t => Math.Abs(t.Amount)).ThenBy(t => t.Account, StringComparer.Ordinal).First();
            totals[totals.IndexOf(largest)] = largest with { Amount = largest.Amount + adjustment };
        }

        return (totals, adjustment);
    }
}
