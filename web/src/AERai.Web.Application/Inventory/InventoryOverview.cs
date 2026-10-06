using AERai.Web.Application.Common;

namespace AERai.Web.Application.Inventory;

/// <summary>What the Inventory page shows: totals for the matching SKUs and one page of them.</summary>
/// <param name="Totals">
/// Totals and status counts across every SKU matching the search and family, before the status
/// filter, so the counts that drive the status filter stay visible while one is applied.
/// </param>
/// <param name="Items">The requested page of SKUs matching every filter, in the requested order.</param>
public sealed record InventoryOverview(InventoryTotals Totals, PagedResult<InventoryItem> Items);
