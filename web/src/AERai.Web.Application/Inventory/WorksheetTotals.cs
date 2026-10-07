namespace AERai.Web.Application.Inventory;

/// <summary>Column totals on the worksheet.</summary>
/// <param name="Sold30d">Units sold in the last 30 days.</param>
/// <param name="Sold90d">Units sold in the last 90 days.</param>
/// <param name="Available">Units sellable at Amazon.</param>
/// <param name="Inbound">Units on their way into Amazon.</param>
/// <param name="SendToAmazon">Units to send from home stock.</param>
/// <param name="HomeStock">Units held at home.</param>
/// <param name="Reorder">Units for the next supplier order, over SKUs whose order is due soon or overdue.</param>
public sealed record WorksheetTotals(int Sold30d, int Sold90d, int Available, int Inbound, int SendToAmazon, int HomeStock, int Reorder)
{
    /// <summary>Totals a set of SKUs.</summary>
    /// <param name="items">The SKUs.</param>
    /// <param name="reorderWithinDays">A supplier order counts toward <see cref="Reorder"/> when due within this many days.</param>
    /// <returns>The totals.</returns>
    public static WorksheetTotals Of(IReadOnlyCollection<InventoryItem> items, int reorderWithinDays)
    {
        ArgumentNullException.ThrowIfNull(items);

        return new(
            items.Sum(i => i.UnitsSold30d),
            items.Sum(i => i.UnitsSold90d),
            items.Sum(i => i.Position.Available),
            items.Sum(i => i.Position.Inbound),
            items.Sum(i => i.Restock?.SendToAmazon ?? 0),
            items.Sum(i => i.Position.HomeStock),
            items.Where(i => i.Restock?.DaysUntilReorder <= reorderWithinDays).Sum(i => i.Restock!.ReorderQuantity)); // Non-null: filtered.
    }
}
