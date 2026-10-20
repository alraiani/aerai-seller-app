namespace AERai.Web.Application.Settlements;

/// <summary>What the Deferred page lists and in what order.</summary>
/// <param name="View">Still held, or recently released.</param>
/// <param name="Sort">Sort column.</param>
/// <param name="Descending">Reverse the column's natural direction.</param>
public sealed record DeferredListFilter(DeferredView View = DeferredView.Deferred, DeferredSort Sort = DeferredSort.Maturity, bool Descending = false);
