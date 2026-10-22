namespace AERai.Web.Application.Settlements;

/// <summary>
/// What Amazon deferred out of one settlement's payout: sales made in its period that it held back, in one
/// currency. The amounts are what will be (or was) paid out for them later, after Amazon's fees.
/// </summary>
/// <param name="SettlementId">The settlement the sales were made in.</param>
/// <param name="Currency">ISO 4217 currency of the amounts.</param>
/// <param name="Amount">Everything deferred out of it (held now or released since).</param>
/// <param name="Count">Deferred transactions.</param>
/// <param name="Orders">Distinct orders among them.</param>
/// <param name="HeldAmount">The part Amazon still holds.</param>
/// <param name="HeldCount">Transactions still held.</param>
/// <param name="FirstRelease">The earliest release date.</param>
/// <param name="LastRelease">The latest release date.</param>
public sealed record SettlementDeferral(
    string SettlementId,
    string Currency,
    decimal Amount,
    int Count,
    int Orders,
    decimal HeldAmount,
    int HeldCount,
    DateTimeOffset? FirstRelease,
    DateTimeOffset? LastRelease);
