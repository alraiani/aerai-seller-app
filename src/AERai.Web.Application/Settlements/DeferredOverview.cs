namespace AERai.Web.Application.Settlements;

/// <summary>The Settlements list's "deferred, not yet paid" tile.</summary>
/// <param name="Held">What Amazon holds, one entry per currency, the marketplace's own currency first.</param>
/// <param name="Sync">How fresh the numbers are.</param>
/// <param name="Now">When the overview was read (release dates before it are due).</param>
public sealed record DeferredOverview(IReadOnlyList<DeferredHeld> Held, DeferredSyncState Sync, DateTimeOffset Now)
{
    /// <summary>Whether a run has ever read Amazon's listing (without one the numbers mean nothing).</summary>
    public bool IsSynced => Sync.LastSuccessAt is not null;

    /// <summary>The soonest release across currencies, when any.</summary>
    public DateTimeOffset? NextMaturity => Held.Where(h => h.NextMaturity is not null).Min(h => h.NextMaturity);

    /// <summary>
    /// Whether the soonest release date has passed. Amazon keeps listing a transaction as deferred until the
    /// money reaches the account balance, so a release date can be a day or two behind.
    /// </summary>
    public bool ReleaseDue => NextMaturity <= Now;

    /// <summary>Whether a release date has passed (the transaction is due to be paid out with the next settlement).</summary>
    /// <param name="maturity">A transaction's release date.</param>
    /// <returns><see langword="true"/> once the date has passed.</returns>
    public bool IsDue(DateTimeOffset? maturity) => maturity <= Now;

    /// <summary>Orders held across currencies.</summary>
    public int Orders => Held.Sum(h => h.Orders);
}
