namespace AERai.Web.Application.Settlements;

/// <summary>One settlement's deferred transactions on the Deferred page.</summary>
/// <param name="SettlementId">The settlement the sales were made in, or <see langword="null"/> when Amazon gave none.</param>
/// <param name="Currency">ISO 4217 currency of the amounts.</param>
/// <param name="Amount">Everything the view selects in it.</param>
/// <param name="Count">Transactions the view selects.</param>
/// <param name="Orders">Distinct orders among them.</param>
/// <param name="HeldAmount">The part Amazon still holds.</param>
/// <param name="HeldCount">Transactions still held.</param>
/// <param name="FirstRelease">The earliest release date.</param>
/// <param name="LastRelease">The latest release date.</param>
/// <param name="FirstPosted">When the earliest of its sales posted.</param>
/// <param name="LastPosted">When the latest of its sales posted.</param>
/// <param name="PeriodStart">The settlement's period start, once it is imported.</param>
/// <param name="PeriodEnd">The settlement's period end, once it is imported.</param>
/// <param name="IsListed">Whether the settlement is in this marketplace's settlements list (so its Summary can be opened).</param>
/// <param name="Rows">Its transactions, soonest release first, at most <see cref="DeferredPaymentService.MaxGroupRows"/>.</param>
public sealed record DeferredSettlementGroup(
    string? SettlementId,
    string Currency,
    decimal Amount,
    int Count,
    int Orders,
    decimal HeldAmount,
    int HeldCount,
    DateTimeOffset? FirstRelease,
    DateTimeOffset? LastRelease,
    DateTimeOffset? FirstPosted,
    DateTimeOffset? LastPosted,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd,
    bool IsListed,
    IReadOnlyList<DeferredPaymentRow> Rows)
{
    /// <summary>Whether the settlement is in the database (so its period is known).</summary>
    public bool IsImported => PeriodEnd is not null;

    /// <summary>
    /// Whether it is the settlement still open: not imported yet and still holding money. A settlement that is
    /// simply missing here (its report never imported) has had everything paid out.
    /// </summary>
    public bool IsOpen => SettlementId is not null && !IsImported && HeldCount > 0;

    /// <summary>Whether the group has more transactions than <see cref="Rows"/> shows.</summary>
    public bool HasMoreRows => Rows.Count < Count;
}
