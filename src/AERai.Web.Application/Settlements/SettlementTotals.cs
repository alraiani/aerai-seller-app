namespace AERai.Web.Application.Settlements;

/// <summary>
/// Totals over the settlements in one currency that a Settlements list filter selects (every page,
/// not just the one shown). Amounts in different currencies are never added together.
/// </summary>
/// <param name="Currency">ISO 4217 currency code of the settlements counted.</param>
/// <param name="Count">Settlements matched.</param>
/// <param name="Sales">Sum of their sales.</param>
/// <param name="Fees">Sum of their fees (negative).</param>
/// <param name="Refunds">Sum of their refunds (usually negative).</param>
/// <param name="Other">Sum of everything else.</param>
/// <param name="Net">Sum of their net payouts.</param>
public sealed record SettlementTotals(string? Currency, int Count, decimal Sales, decimal Fees, decimal Refunds, decimal Other, decimal Net)
{
    /// <summary>No settlements in a currency.</summary>
    /// <param name="currency">The currency.</param>
    /// <returns>Zero totals.</returns>
    public static SettlementTotals Empty(string? currency) => new(currency, 0, 0, 0, 0, 0, 0);

    /// <summary>Fees as a share of sales (0.15 = 15 %), or <see langword="null"/> without sales.</summary>
    public decimal? FeeRate => Sales > 0 ? -Fees / Sales : null;

    /// <summary>Refunds as a share of sales, or <see langword="null"/> without sales.</summary>
    public decimal? RefundRate => Sales > 0 ? -Refunds / Sales : null;
}
