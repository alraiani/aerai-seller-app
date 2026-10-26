namespace AERai.Web.Application.Advertising;

/// <summary>One product family's advertised SKUs and their subtotal.</summary>
/// <param name="FamilyId">Family id, or <see langword="null"/> for SKUs without a family.</param>
/// <param name="Family">Family name, or <see langword="null"/> for SKUs without a family.</param>
/// <param name="Skus">The family's advertised SKUs, in the chosen order.</param>
/// <param name="Totals">The sum of <paramref name="Skus"/>.</param>
public sealed record AdFamilyGroup(int? FamilyId, string? Family, IReadOnlyList<AdSkuRow> Skus, AdMetrics Totals);
