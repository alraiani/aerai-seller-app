namespace AERai.Web.Application.Advertising;

/// <summary>What the Advertising page shows.</summary>
/// <param name="From">First day, in the marketplace's local time.</param>
/// <param name="To">Last day (inclusive), in the marketplace's local time.</param>
/// <param name="FamilyId">Only this family's SKUs; <c>0</c> for SKUs without a family; <see langword="null"/> for all.</param>
/// <param name="Search">Words that must each match a SKU, ASIN, title, or family name.</param>
/// <param name="Sort">How SKUs are ordered within each family.</param>
/// <param name="Descending">Reverses <paramref name="Sort"/>'s natural direction when it differs from it.</param>
public sealed record AdvertisingFilter(DateOnly From, DateOnly To, int? FamilyId, string? Search, AdSort Sort, bool Descending)
{
    /// <summary>The family id that selects SKUs without a family.</summary>
    public const int NoFamily = 0;
}
