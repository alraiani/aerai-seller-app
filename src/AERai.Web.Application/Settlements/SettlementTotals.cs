namespace AERai.Web.Application.Settlements;

/// <summary>
/// Totals over the settlements a Settlements list filter selects (every page, not just the one shown),
/// in the marketplace's currency: settlements paid in another currency count at their converted amounts.
/// Settlements still waiting for an exchange rate are counted apart and left out of the sums, so
/// amounts in different currencies are never added together.
/// </summary>
/// <param name="Currency">ISO 4217 currency of the sums (the marketplace's).</param>
/// <param name="Count">Settlements in the sums.</param>
/// <param name="AwaitingRate">Settlements matched but left out because they have no exchange rate yet.</param>
/// <param name="Sales">Sum of their sales.</param>
/// <param name="Fees">Sum of their fees (negative).</param>
/// <param name="Refunds">Sum of their refunds (usually negative).</param>
/// <param name="Other">Sum of everything else.</param>
/// <param name="Net">Sum of their net payouts.</param>
public sealed record SettlementTotals(string Currency, int Count, int AwaitingRate, decimal Sales, decimal Fees, decimal Refunds, decimal Other, decimal Net)
{
    /// <summary>No settlements.</summary>
    /// <param name="currency">The marketplace's currency.</param>
    /// <returns>Zero totals.</returns>
    public static SettlementTotals Empty(string currency) => new(currency, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Fees as a share of sales (0.15 = 15 %), or <see langword="null"/> without sales.</summary>
    public decimal? FeeRate => Sales > 0 ? -Fees / Sales : null;

    /// <summary>Refunds as a share of sales, or <see langword="null"/> without sales.</summary>
    public decimal? RefundRate => Sales > 0 ? -Refunds / Sales : null;
}
