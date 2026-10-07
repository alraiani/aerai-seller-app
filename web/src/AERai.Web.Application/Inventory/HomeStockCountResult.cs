namespace AERai.Web.Application.Inventory;

/// <summary>What applying a count sheet did.</summary>
/// <param name="Applied">Ledger entries written (one per changed SKU).</param>
/// <param name="UnitsIn">Units added.</param>
/// <param name="UnitsOut">Units removed (positive).</param>
/// <param name="Stale">
/// SKUs skipped because their home stock changed between the review and Apply, so the reviewed
/// difference no longer holds.
/// </param>
public sealed record HomeStockCountResult(int Applied, int UnitsIn, int UnitsOut, IReadOnlyList<string> Stale);
