namespace AERai.Web.Application.Advertising;

/// <summary>What the Advertising page shows for a marketplace and range of days.</summary>
/// <param name="From">First day shown.</param>
/// <param name="To">Last day shown.</param>
/// <param name="Groups">One group per family with advertised SKUs, by family name, SKUs without a family last.</param>
/// <param name="Totals">The sum of every group.</param>
/// <param name="SpendFrom">The first day with ad spend stored for the marketplace (any range), or <see langword="null"/> when none has been synced.</param>
/// <param name="SpendThrough">The latest day with ad spend stored for the marketplace (any range), or <see langword="null"/> when none has been synced.</param>
public sealed record AdvertisingReport(
    DateOnly From, DateOnly To, IReadOnlyList<AdFamilyGroup> Groups, AdMetrics Totals, DateOnly? SpendFrom, DateOnly? SpendThrough)
{
    /// <summary>
    /// Whether the range starts before the first synced day of spend. Total sales (and so TACoS) then start at
    /// <see cref="SpendFrom"/> too, so days without synced spend don't make advertising look cheaper.
    /// </summary>
    public bool StartsBeforeSync => SpendFrom is { } first && From < first;

    /// <summary>Advertised SKUs listed.</summary>
    public int SkuCount => Groups.Sum(g => g.Skus.Count);
}
