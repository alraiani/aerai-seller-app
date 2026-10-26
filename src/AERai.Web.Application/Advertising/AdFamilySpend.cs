namespace AERai.Web.Application.Advertising;

/// <summary>What one product family spent on Sponsored Products over a range of days.</summary>
/// <param name="FamilyId">The family, or <see langword="null"/> for advertised SKUs without one.</param>
/// <param name="Family">The family's name, or <see langword="null"/> without one.</param>
/// <param name="Spend">Ad spend (positive), in the marketplace's currency.</param>
public sealed record AdFamilySpend(int? FamilyId, string? Family, decimal Spend);
