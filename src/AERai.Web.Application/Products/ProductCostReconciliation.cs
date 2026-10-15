using AERai.Web.Application.Imports;

namespace AERai.Web.Application.Products;

/// <summary>
/// An uploaded cost sheet checked against current costs, before anything is saved: which costs
/// would be set or changed, and which rows can't be used.
/// </summary>
/// <param name="Changes">SKUs whose cost differs from the sheet, newly set ones first.</param>
/// <param name="UnchangedCount">Valid rows whose cost already matches.</param>
/// <param name="BlankCount">Rows with no cost, which leave the SKU's cost as it is.</param>
/// <param name="Rejected">Rows that can't be used, with the reason.</param>
public sealed record ProductCostReconciliation(IReadOnlyList<ProductCostChange> Changes, int UnchangedCount, int BlankCount, IReadOnlyList<RejectedRow> Rejected)
{
    /// <summary>SKUs getting a cost for the first time.</summary>
    public int NewCount => Changes.Count(c => c.Current is null);

    /// <summary>SKUs whose existing cost changes.</summary>
    public int UpdatedCount => Changes.Count(c => c.Current is not null);
}
