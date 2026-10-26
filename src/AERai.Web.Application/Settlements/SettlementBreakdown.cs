using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Settlements;

/// <summary>
/// A settlement row's amounts by category, in the currency a page shows them in: refunds split into
/// refunded sales and refunded expenses, and expenses into shipping, FBA fees, commission, and the rest.
/// The parts of a bucket always add up to it (the views make sure of that, converted amounts included).
/// </summary>
/// <param name="Sales">Sales.</param>
/// <param name="RefundedSales">Price, shipping, and tax given back on refunds (negative).</param>
/// <param name="RefundedExpenses">Fees given back or charged on refunds.</param>
/// <param name="Shipping">Shipping to customers and into Amazon (negative).</param>
/// <param name="FbaFees">Every FBA fee (negative).</param>
/// <param name="Commission">Referral fees (negative).</param>
/// <param name="OtherExpenses">Every other charge (advertising, subscriptions, promotions, …).</param>
/// <param name="Reserve">Account level reserve.</param>
/// <param name="Net">Everything together.</param>
/// <param name="Currency">ISO 4217 currency of the amounts.</param>
/// <param name="IsConverted">Whether they were converted from the currency the settlement was paid in.</param>
public sealed record SettlementBreakdown(
    decimal Sales,
    decimal RefundedSales,
    decimal RefundedExpenses,
    decimal Shipping,
    decimal FbaFees,
    decimal Commission,
    decimal OtherExpenses,
    decimal Reserve,
    decimal Net,
    string? Currency,
    bool IsConverted)
{
    /// <summary>Refunded sales and refunded expenses together.</summary>
    public decimal Refunds => RefundedSales + RefundedExpenses;

    /// <summary>Shipping, FBA fees, commission, and other expenses together.</summary>
    public decimal Expenses => Shipping + FbaFees + Commission + OtherExpenses;

    /// <summary>
    /// A settlement's amounts as asked for: converted only when the settlement has a rate, otherwise as
    /// paid (see <see cref="SettlementMoney.UseHome"/>).
    /// </summary>
    /// <param name="row">The settlement.</param>
    /// <param name="amounts">Which currency is wanted.</param>
    /// <returns>The amounts to show.</returns>
    public static SettlementBreakdown Of(SettlementSummary row, SettlementAmounts amounts)
    {
        ArgumentNullException.ThrowIfNull(row);
        return SettlementMoney.UseHome(amounts, row.ExchangeRate)
            ? new(row.HomeSales ?? 0, row.HomeRefundedSales ?? 0, row.HomeRefundedExpenses ?? 0, row.HomeShipping ?? 0, row.HomeFbaFees ?? 0,
                row.HomeCommission ?? 0, row.HomeOtherExpenses ?? 0, row.HomeReserve ?? 0, row.HomeNet ?? 0, row.HomeCurrency, Differs(row.Currency, row.HomeCurrency))
            : new(row.Sales, row.RefundedSales, row.RefundedExpenses, row.Shipping, row.FbaFees,
                row.Commission, row.OtherExpenses, row.Reserve, row.Net, row.Currency, false);
    }

    /// <summary>A per-SKU summary row's amounts as asked for (see <see cref="Of(SettlementSummary, SettlementAmounts)"/>).</summary>
    /// <param name="row">The row.</param>
    /// <param name="amounts">Which currency is wanted.</param>
    /// <returns>The amounts to show.</returns>
    public static SettlementBreakdown Of(SettlementSkuSummary row, SettlementAmounts amounts)
    {
        ArgumentNullException.ThrowIfNull(row);
        return SettlementMoney.UseHome(amounts, row.ExchangeRate)
            ? new(row.HomeSales ?? 0, row.HomeRefundedSales ?? 0, row.HomeRefundedExpenses ?? 0, row.HomeShipping ?? 0, row.HomeFbaFees ?? 0,
                row.HomeCommission ?? 0, row.HomeOtherExpenses ?? 0, row.HomeReserve ?? 0, row.HomeNet ?? 0, row.HomeCurrency, Differs(row.Currency, row.HomeCurrency))
            : new(row.Sales, row.RefundedSales, row.RefundedExpenses, row.Shipping, row.FbaFees,
                row.Commission, row.OtherExpenses, row.Reserve, row.Net, row.Currency, false);
    }

    private static bool Differs(string? currency, string homeCurrency) =>
        currency is not null && !string.Equals(currency, homeCurrency, StringComparison.OrdinalIgnoreCase);
}
