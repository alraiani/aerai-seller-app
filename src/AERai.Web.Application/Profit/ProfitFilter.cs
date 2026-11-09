namespace AERai.Web.Application.Profit;

/// <summary>What the profit page shows.</summary>
/// <param name="From">First day, in the marketplace's local time.</param>
/// <param name="To">Last day (inclusive), in the marketplace's local time.</param>
/// <param name="FamilyId">Only this family's SKUs; <see cref="NoFamily"/> for SKUs without one; <see langword="null"/> for all.</param>
/// <param name="Search">Words that must each match a SKU, ASIN, title, or family name.</param>
/// <param name="Show">Which SKUs to list.</param>
/// <param name="Sort">How SKUs are ordered within each family.</param>
/// <param name="Descending">Reverses <paramref name="Sort"/>'s natural direction when it differs from it.</param>
/// <param name="FullyLoaded">Spreads amounts not tied to a SKU (subscriptions, storage) across SKUs by sales share.</param>
public sealed record ProfitFilter(
    DateOnly From, DateOnly To, int? FamilyId, string? Search, ProfitShow Show, ProfitSort Sort, bool Descending, bool FullyLoaded)
{
    /// <summary>The family id that selects SKUs without a family.</summary>
    public const int NoFamily = 0;

    /// <summary>Whether the filter narrows the SKUs, so amounts not tied to a SKU are left out of the totals.</summary>
    public bool NarrowsSkus => FamilyId is not null || !string.IsNullOrWhiteSpace(Search) || Show != ProfitShow.All;
}
