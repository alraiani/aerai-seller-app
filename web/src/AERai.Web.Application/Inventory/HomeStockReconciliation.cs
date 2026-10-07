using AERai.Web.Application.Imports;

namespace AERai.Web.Application.Inventory;

/// <summary>
/// An uploaded count sheet checked against current home stock, before anything is saved: what
/// would go up, what would go down, and which rows can't be used.
/// </summary>
/// <param name="Changes">SKUs whose count differs, largest change first.</param>
/// <param name="UnchangedCount">Valid rows whose count already matches.</param>
/// <param name="Rejected">Rows that can't be used, with the reason.</param>
public sealed record HomeStockReconciliation(IReadOnlyList<HomeStockCountChange> Changes, int UnchangedCount, IReadOnlyList<RejectedRow> Rejected)
{
    /// <summary>SKUs going up.</summary>
    public int IncreaseCount => Changes.Count(c => c.Difference > 0);

    /// <summary>SKUs going down.</summary>
    public int DecreaseCount => Changes.Count(c => c.Difference < 0);

    /// <summary>Units added across all increases.</summary>
    public int UnitsIn => Changes.Where(c => c.Difference > 0).Sum(c => c.Difference);

    /// <summary>Units removed across all decreases (positive).</summary>
    public int UnitsOut => -Changes.Where(c => c.Difference < 0).Sum(c => c.Difference);
}
