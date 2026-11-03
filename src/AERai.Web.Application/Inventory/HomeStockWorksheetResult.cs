namespace AERai.Web.Application.Inventory;

/// <summary>What a worksheet save did.</summary>
/// <param name="Changed">The home-stock changes logged, or <see langword="null"/> when there were none.</param>
/// <param name="Shipped">The sends logged, or <see langword="null"/> when there were none or they failed.</param>
/// <param name="SendError">
/// Why the sends were not logged after the changes were (they are saved apart, so the changes stand);
/// <see langword="null"/> when they were, or there were none.
/// </param>
public sealed record HomeStockWorksheetResult(HomeStockCountResult? Changed, HomeStockCountResult? Shipped, string? SendError)
{
    /// <summary>SKUs skipped because their home stock changed after the page loaded.</summary>
    public IReadOnlyList<string> Stale =>
        [.. (Changed?.Stale ?? []).Concat(Shipped?.Stale ?? []).Distinct(StringComparer.Ordinal)];
}
