namespace AERai.Web.Application.Inventory;

/// <summary>The outcome of a bulk picture upload or an Amazon picture pull.</summary>
/// <param name="Outcomes">One entry per file or ASIN, in the order processed.</param>
/// <param name="Remaining">SKUs left for the next Amazon pull (each pull downloads a limited number).</param>
/// <param name="WithoutAsin">SKUs without a picture that Amazon can't help with because they have no ASIN.</param>
public sealed record PictureImportResult(IReadOnlyList<PictureImportOutcome> Outcomes, int Remaining = 0, int WithoutAsin = 0)
{
    /// <summary>SKUs that got a picture (added or replaced).</summary>
    public int StoredSkus => Outcomes.Where(o => o.Status is PictureImportStatus.Added or PictureImportStatus.Replaced).Sum(o => o.Skus.Count);

    /// <summary>How many outcomes have a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The count.</returns>
    public int Count(PictureImportStatus status) => Outcomes.Count(o => o.Status == status);
}
