namespace AERai.Web.UI.Models;

/// <summary>Data for the inventory section tab bar.</summary>
/// <param name="Current">The page being shown.</param>
/// <param name="FamilyId">The family in view, carried to the pages that filter by family.</param>
public sealed record InventoryNavModel(InventorySection Current, int? FamilyId = null);
