namespace AERai.Web.Application.Settlements;

/// <summary>What the Deferred page lists and in what order.</summary>
/// <param name="View">Still held, released, or both.</param>
/// <param name="Sort">Sort column.</param>
/// <param name="Descending">Reverse the column's natural direction.</param>
/// <param name="SettlementId">
/// Only transactions sold in this settlement, <see cref="NoSettlement"/> for those Amazon gave none, or
/// <see langword="null"/> for every settlement.
/// </param>
public sealed record DeferredListFilter(
    DeferredView View = DeferredView.Deferred, DeferredSort Sort = DeferredSort.Maturity, bool Descending = false, string? SettlementId = null)
{
    /// <summary>The <see cref="SettlementId"/> that selects transactions without a settlement.</summary>
    public const string NoSettlement = "none";
}
