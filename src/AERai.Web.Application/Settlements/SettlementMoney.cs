using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Settlements;

/// <summary>A settlement row's amounts in the currency a page shows them in.</summary>
/// <param name="Sales">Sales.</param>
/// <param name="Expenses">Expenses (negative).</param>
/// <param name="Refunds">Refunds (usually negative).</param>
/// <param name="Reserve">Account level reserve.</param>
/// <param name="Net">The four together.</param>
/// <param name="Currency">ISO 4217 currency of the amounts.</param>
/// <param name="IsConverted">Whether they were converted from the currency the settlement was paid in.</param>
public sealed record SettlementMoney(decimal Sales, decimal Expenses, decimal Refunds, decimal Reserve, decimal Net, string? Currency, bool IsConverted)
{
    /// <summary>
    /// A settlement's amounts as asked for. Converted amounts are only shown when the settlement has a
    /// rate; until then it is shown as paid (and flagged as waiting for a rate) rather than guessed.
    /// </summary>
    /// <param name="row">The settlement.</param>
    /// <param name="amounts">Which currency is wanted.</param>
    /// <returns>The amounts to show.</returns>
    public static SettlementMoney Of(SettlementSummary row, SettlementAmounts amounts)
    {
        ArgumentNullException.ThrowIfNull(row);
        return UseHome(amounts, row.ExchangeRate)
            ? new(row.HomeSales ?? 0, row.HomeExpenses ?? 0, row.HomeRefunds ?? 0, row.HomeReserve ?? 0, row.HomeNet ?? 0, row.HomeCurrency, Differs(row.Currency, row.HomeCurrency))
            : new(row.Sales, row.Expenses, row.Refunds, row.Reserve, row.Net, row.Currency, false);
    }

    /// <summary>A per-SKU summary row's amounts as asked for (see <see cref="Of(SettlementSummary, SettlementAmounts)"/>).</summary>
    /// <param name="row">The row.</param>
    /// <param name="amounts">Which currency is wanted.</param>
    /// <returns>The amounts to show.</returns>
    public static SettlementMoney Of(SettlementSkuSummary row, SettlementAmounts amounts)
    {
        ArgumentNullException.ThrowIfNull(row);
        return UseHome(amounts, row.ExchangeRate)
            ? new(row.HomeSales ?? 0, row.HomeExpenses ?? 0, row.HomeRefunds ?? 0, row.HomeReserve ?? 0, row.HomeNet ?? 0, row.HomeCurrency, Differs(row.Currency, row.HomeCurrency))
            : new(row.Sales, row.Expenses, row.Refunds, row.Reserve, row.Net, row.Currency, false);
    }

    /// <summary>A settlement line's amount as asked for (see <see cref="Of(SettlementSummary, SettlementAmounts)"/>).</summary>
    /// <param name="line">The line.</param>
    /// <param name="amounts">Which currency is wanted.</param>
    /// <returns>The amount and its currency.</returns>
    public static (decimal Amount, string? Currency) AmountOf(SettlementLineDetail line, SettlementAmounts amounts)
    {
        ArgumentNullException.ThrowIfNull(line);
        return UseHome(amounts, line.ExchangeRate)
            ? (line.HomeAmount ?? 0, line.HomeCurrency)
            : (line.Amount, line.Currency);
    }

    /// <summary>The price per item as asked for (see <see cref="Of(SettlementSummary, SettlementAmounts)"/>).</summary>
    /// <param name="row">The row.</param>
    /// <param name="amounts">Which currency is wanted.</param>
    /// <returns>The price, or <see langword="null"/> when the row has none.</returns>
    public static decimal? SalePriceOf(SettlementSkuSummary row, SettlementAmounts amounts)
    {
        ArgumentNullException.ThrowIfNull(row);
        return UseHome(amounts, row.ExchangeRate) ? row.HomeSalePrice : row.SalePrice;
    }

    /// <summary>Whether converted amounts are shown: asked for and a rate is known.</summary>
    /// <param name="amounts">Which currency is wanted.</param>
    /// <param name="rate">The settlement's rate, or <see langword="null"/> while unknown.</param>
    /// <returns><see langword="true"/> to show the Home columns.</returns>
    public static bool UseHome(SettlementAmounts amounts, decimal? rate) =>
        amounts == SettlementAmounts.Home && rate is not null;

    private static bool Differs(string? currency, string homeCurrency) =>
        currency is not null && !string.Equals(currency, homeCurrency, StringComparison.OrdinalIgnoreCase);
}
