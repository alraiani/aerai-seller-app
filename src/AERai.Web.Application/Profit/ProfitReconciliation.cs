namespace AERai.Web.Application.Profit;

/// <summary>How the profit page lines up with the settlements Amazon paid.</summary>
/// <param name="Settlements">Each settlement whose period ends in the range, against the ledger.</param>
/// <param name="Bridge">From what Amazon paid in the range to the profit made in it.</param>
/// <param name="OtherExpenseKinds">Ledger amounts counted as other expenses, so a new kind of fee is seen, not lost.</param>
/// <param name="LedgerStartsOn">The first local day read from the ledger, or <see langword="null"/> without one.</param>
public sealed record ProfitReconciliation(
    IReadOnlyList<SettlementTie> Settlements, ProfitBridge Bridge, IReadOnlyList<ProfitUnmappedRow> OtherExpenseKinds, DateOnly? LedgerStartsOn);

/// <summary>One settlement next to the ledger.</summary>
/// <param name="Row">The settlement and the ledger's total for it.</param>
/// <param name="State">Whether they agree.</param>
/// <param name="Difference">Settlement minus ledger, when the ledger covers it.</param>
public sealed record SettlementTie(SettlementTieRow Row, SettlementTieState State, decimal? Difference);

/// <summary>Whether a settlement agrees with the ledger.</summary>
public enum SettlementTieState
{
    /// <summary>The ledger's transactions for it add up to what it paid, to the cent.</summary>
    Ties = 1,

    /// <summary>They don't (the difference is shown).</summary>
    Differs = 2,

    /// <summary>The settlement began before the ledger did, so the ledger can't hold all of it.</summary>
    BeforeLedger = 3,
}

/// <summary>
/// From what Amazon paid out in a range to the profit made in it. The lines add up:
/// <c>Payouts − Reserve + Timing + AdvertisingAdjustment + Cogs = Profit</c>.
/// </summary>
/// <param name="Payouts">What the settlements whose period ends in the range paid.</param>
/// <param name="Reserve">The reserve movements inside those payouts (not profit).</param>
/// <param name="Timing">Amounts posted in the range but paid outside it, less those paid in it but posted before.</param>
/// <param name="AdvertisingAdjustment">Ads API spend in place of the ad charges Amazon billed.</param>
/// <param name="Cogs">Cost of goods (net of refunds).</param>
/// <param name="Profit">The profit for the range.</param>
public sealed record ProfitBridge(decimal Payouts, decimal Reserve, decimal Timing, decimal AdvertisingAdjustment, decimal Cogs, decimal Profit);
