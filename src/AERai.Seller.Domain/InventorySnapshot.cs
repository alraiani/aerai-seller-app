namespace AERai.Seller.Domain;

/// <summary>
/// One quantity reading for a SKU in a given state as of a given date.
/// Modeled per-SKU/per-state from the start so demand forecasting and
/// replenishment planning (Phase 2) don't require a schema rework.
/// </summary>
public class InventorySnapshot
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public InventoryState State { get; set; }
    public int Quantity { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public DateTimeOffset SyncedAt { get; set; }
}
