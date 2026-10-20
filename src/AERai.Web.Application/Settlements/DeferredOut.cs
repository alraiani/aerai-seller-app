namespace AERai.Web.Application.Settlements;

/// <summary>
/// What Amazon deferred out of one settlement's payout, in the currency the settlement page shows (converted
/// at the settlement's rate when it shows the marketplace's currency).
/// </summary>
/// <param name="Amount">Everything deferred out of it.</param>
/// <param name="HeldAmount">The part Amazon still holds.</param>
/// <param name="Currency">ISO 4217 currency of the amounts.</param>
/// <param name="Count">Deferred transactions.</param>
/// <param name="Orders">Distinct orders.</param>
/// <param name="HeldCount">Transactions still held.</param>
/// <param name="FirstRelease">The earliest release date.</param>
/// <param name="LastRelease">The latest release date.</param>
public sealed record DeferredOut(
    decimal Amount,
    decimal HeldAmount,
    string Currency,
    int Count,
    int Orders,
    int HeldCount,
    DateTimeOffset? FirstRelease,
    DateTimeOffset? LastRelease)
{
    /// <summary>Whether all of it has been paid out since.</summary>
    public bool IsAllReleased => HeldCount == 0;
}
