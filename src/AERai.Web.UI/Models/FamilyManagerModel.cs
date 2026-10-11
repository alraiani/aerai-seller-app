using AERai.Web.Application.Inventory;

namespace AERai.Web.UI.Models;

/// <summary>Data for the shared <c>_FamilyManager</c> partial (the Families dialog and page).</summary>
/// <param name="Families">Families with their SKU counts.</param>
/// <param name="Back">The Inventory list's query string, so every action returns to the same view.</param>
public sealed record FamilyManagerModel(IReadOnlyList<FamilySummary> Families, string Back);
