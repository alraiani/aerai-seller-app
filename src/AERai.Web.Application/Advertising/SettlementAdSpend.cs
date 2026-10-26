namespace AERai.Web.Application.Advertising;

/// <summary>
/// Sponsored Products spend by family over the days a list of settlements covers, from the Ads API
/// (<c>core.AdSpend</c>). Settlements carry advertising only as one charge per invoice with no SKU, so
/// this is what splits it by family; it is spend as Amazon Ads reports it, not the amount charged, which
/// Amazon bills later and in lump sums.
/// </summary>
/// <param name="From">First local day added up, or <see langword="null"/> when no settlement is listed.</param>
/// <param name="To">Last local day added up, or <see langword="null"/> when no settlement is listed.</param>
/// <param name="Families">Spend per family, by family name, SKUs without a family last; families without spend are absent.</param>
/// <param name="SpendFrom">The first day with ad spend stored for the marketplace, or <see langword="null"/> when none has been synced.</param>
/// <param name="SpendThrough">The latest day with ad spend stored for the marketplace, or <see langword="null"/> when none has been synced.</param>
public sealed record SettlementAdSpend(
    DateOnly? From, DateOnly? To, IReadOnlyList<AdFamilySpend> Families, DateOnly? SpendFrom, DateOnly? SpendThrough)
{
    /// <summary>No settlements listed and no spend synced.</summary>
    public static SettlementAdSpend None { get; } = new(null, null, [], null, null);

    /// <summary>Whether any ad spend has been synced for the marketplace.</summary>
    public bool IsSynced => SpendFrom is not null;

    /// <summary>The spend of every family together.</summary>
    public decimal Total => Families.Sum(f => f.Spend);

    /// <summary>Whether the settlements start before the first synced day, so their earliest days have no spend to show.</summary>
    public bool StartsBeforeSync => From is { } from && SpendFrom is { } first && from < first;

    /// <summary>Whether the settlements end after the last synced day, so their latest days have no spend yet.</summary>
    public bool EndsAfterSync => To is { } to && SpendThrough is { } last && to > last;
}
