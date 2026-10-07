using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>The SKUs of one color on the worksheet.</summary>
/// <param name="Color">The color, or <see langword="null"/> for SKUs without one.</param>
/// <param name="Items">The SKUs, by SKU.</param>
/// <param name="Subtotals">Totals over <paramref name="Items"/>.</param>
public sealed record WorksheetGroup(ProductColor? Color, IReadOnlyList<InventoryItem> Items, WorksheetTotals Subtotals);
