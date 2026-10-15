namespace AERai.Web.Application.Products;

/// <summary>What saving a set of cost changes did.</summary>
/// <param name="Applied">SKUs whose cost was saved.</param>
/// <param name="Stale">
/// SKUs skipped because their cost changed after the page or sheet was read, so the change would
/// overwrite someone else's edit unseen.
/// </param>
public sealed record ProductCostResult(int Applied, IReadOnlyList<string> Stale);
