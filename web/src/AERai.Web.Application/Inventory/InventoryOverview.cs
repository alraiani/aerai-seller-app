using AERai.Web.Application.Common;

namespace AERai.Web.Application.Inventory;

/// <summary>What the Inventory page shows: totals for the matching SKUs and one page of them.</summary>
/// <param name="Totals">Totals across every SKU matching the search, not just this page.</param>
/// <param name="Items">The requested page, most urgent (fewest days of inventory) first.</param>
public sealed record InventoryOverview(InventoryTotals Totals, PagedResult<InventoryItem> Items);
