namespace AERai.Web.Application.Inventory;

/// <summary>Narrows a list of open sends.</summary>
/// <param name="FamilyId">Only this family's SKUs, or all when <see langword="null"/>.</param>
/// <param name="Search">Text matched against the SKU, product title, and reference (ignoring case), or <see langword="null"/>.</param>
/// <param name="View">Which sends to show.</param>
public sealed record PendingSendFilter(int? FamilyId = null, string? Search = null, PendingSendView View = PendingSendView.All)
{
    /// <summary>The whole list, for one family or all.</summary>
    /// <param name="familyId">The family, or <see langword="null"/> for all.</param>
    /// <returns>The filter.</returns>
    public static PendingSendFilter ForFamily(int? familyId) => new(familyId);
}
