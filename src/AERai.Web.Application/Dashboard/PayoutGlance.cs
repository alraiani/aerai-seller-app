namespace AERai.Web.Application.Dashboard;

/// <summary>The most recent settlement payout in the dashboard currency.</summary>
/// <param name="SettlementId">Amazon settlement id.</param>
/// <param name="PeriodStart">Settlement period start.</param>
/// <param name="PeriodEnd">Settlement period end.</param>
/// <param name="Net">Payout amount.</param>
/// <param name="Sales">Item sales in the settlement.</param>
/// <param name="Fees">Amazon fees (negative).</param>
/// <param name="Refunds">Refunds (negative).</param>
/// <param name="Currency">ISO currency code.</param>
public sealed record PayoutGlance(string SettlementId, DateTimeOffset? PeriodStart, DateTimeOffset? PeriodEnd, decimal Net, decimal Sales, decimal Fees, decimal Refunds, string Currency)
{
    /// <summary>Fees as a fraction of sales (0.4 = 40%); <see langword="null"/> without sales.</summary>
    public decimal? FeeRate => Sales == 0 ? null : -Fees / Sales;
}
