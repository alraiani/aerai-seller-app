namespace AERai.Web.Domain.Core;

/// <summary>
/// Quantity of a SKU in one fulfillment state on a given date. Unique per (date, SKU, state).
/// </summary>
public sealed class InventorySnapshot
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>The date the quantities were reported for.</summary>
    public DateOnly SnapshotDate { get; set; }

    /// <summary>Marketplace whose fulfillment network holds the stock.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>Fulfillment state, normalized to one of <see cref="InventoryStates"/>.</summary>
    public required string State { get; set; }

    /// <summary>Units in this state.</summary>
    public int Quantity { get; set; }

    /// <summary>The staging batch that last wrote this row (lineage).</summary>
    public long LastImportBatchId { get; set; }
}
