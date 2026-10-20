namespace AERai.Web.Application.Settlements;

/// <summary>
/// The orders in one settlement whose payout Amazon deferred before releasing it into this settlement.
/// </summary>
/// <param name="Orders">Distinct orders.</param>
/// <param name="Amounts">What their deferred transactions totalled, per currency.</param>
public sealed record SettlementDeferrals(int Orders, IReadOnlyList<(string Currency, decimal Amount)> Amounts)
{
    /// <summary>No deferred orders.</summary>
    public static SettlementDeferrals None { get; } = new(0, []);
}
