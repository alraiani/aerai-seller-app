namespace AERai.Web.Application.Dashboard;

/// <summary>The most recent settlement payout in the dashboard currency (converted when it was paid in another).</summary>
/// <param name="SettlementId">Amazon settlement id.</param>
/// <param name="PeriodStart">Settlement period start.</param>
/// <param name="PeriodEnd">Settlement period end.</param>
/// <param name="Net">Payout amount.</param>
/// <param name="Sales">Item sales in the settlement.</param>
/// <param name="Expenses">Everything Amazon charged (negative), as Seller Central's statement counts it.</param>
/// <param name="Refunds">Refunds (negative).</param>
/// <param name="Currency">ISO currency code of the amounts (the marketplace's).</param>
/// <param name="Origin">The marketplace the payout came from when it was paid in another currency (e.g. <c>CA</c>), otherwise <see langword="null"/>.</param>
public sealed record PayoutGlance(string SettlementId, DateTimeOffset? PeriodStart, DateTimeOffset? PeriodEnd, decimal Net, decimal Sales, decimal Expenses, decimal Refunds, string Currency, string? Origin = null)
{
    /// <summary>
    /// What Amazon still holds of the sales made in the payout's period (deferred out of it, paid in a later one),
    /// or <see langword="null"/> when nothing is held.
    /// </summary>
    public decimal? DeferredHeld { get; init; }

    /// <summary>When the last of <see cref="DeferredHeld"/> is due to be released.</summary>
    public DateTimeOffset? DeferredUntil { get; init; }

    /// <summary>Expenses as a fraction of sales (0.4 = 40%); <see langword="null"/> without sales.</summary>
    public decimal? ExpenseRate => Sales == 0 ? null : -Expenses / Sales;
}
