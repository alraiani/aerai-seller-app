namespace AERai.Web.Application.Settlements;

/// <summary>Totals over the settlements a Settlements list filter selects (every page, not just the one shown).</summary>
/// <param name="Count">Settlements matched.</param>
/// <param name="Sales">Sum of their sales.</param>
/// <param name="Fees">Sum of their fees (negative).</param>
/// <param name="Refunds">Sum of their refunds (usually negative).</param>
/// <param name="Other">Sum of everything else.</param>
/// <param name="Net">Sum of their net payouts.</param>
public sealed record SettlementTotals(int Count, decimal Sales, decimal Fees, decimal Refunds, decimal Other, decimal Net)
{
    /// <summary>No settlements.</summary>
    public static SettlementTotals Empty { get; } = new(0, 0, 0, 0, 0, 0);

    /// <summary>Fees as a share of sales (0.15 = 15 %), or <see langword="null"/> without sales.</summary>
    public decimal? FeeRate => Sales > 0 ? -Fees / Sales : null;

    /// <summary>Refunds as a share of sales, or <see langword="null"/> without sales.</summary>
    public decimal? RefundRate => Sales > 0 ? -Refunds / Sales : null;
}
