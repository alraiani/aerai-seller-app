namespace AERai.Web.Application.Inventory;

/// <summary>
/// A family's SKUs laid out like the team's printed inventory worksheet: grouped by color, with
/// sales, stock, what to send into Amazon, home stock, and the next supplier order.
/// </summary>
/// <param name="FamilyId">The family shown.</param>
/// <param name="Groups">Color groups in palette order, SKUs without a color last.</param>
/// <param name="Totals">Totals over every SKU in the family.</param>
public sealed record InventoryWorksheet(int FamilyId, IReadOnlyList<WorksheetGroup> Groups, WorksheetTotals Totals)
{
    /// <summary>Whether the family has no SKUs in this marketplace.</summary>
    public bool IsEmpty => Groups.Count == 0;
}
