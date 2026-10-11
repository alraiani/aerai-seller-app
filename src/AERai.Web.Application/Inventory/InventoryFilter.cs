namespace AERai.Web.Application.Inventory;

/// <summary>Which SKUs an inventory overview lists, and in what order (search and paging are in the page request).</summary>
/// <param name="FamilyId">Only SKUs in this family, or <see langword="null"/> for all.</param>
/// <param name="Status">Only SKUs in this status group.</param>
/// <param name="Sort">Order.</param>
/// <param name="Descending">Reverse the sort's natural direction.</param>
public sealed record InventoryFilter(int? FamilyId = null, StockStatusFilter Status = StockStatusFilter.All, InventorySort Sort = InventorySort.Urgency, bool Descending = false)
{
    /// <summary>No filter, most urgent first.</summary>
    public static InventoryFilter Default { get; } = new();
}
