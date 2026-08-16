namespace AERai.Seller.Domain.Staging;

/// <summary>
/// One quantity reading for a SKU held in Amazon Warehousing and Distribution (AWD) as of a
/// given date. Parallels <see cref="InventorySnapshot"/> for FBA, but AWD's API returns a fixed
/// set of named quantities rather than a per-state breakdown, so this is a separate shape keyed
/// by Sku+SnapshotDate instead of Sku+State+SnapshotDate.
/// </summary>
public class AwdInventorySnapshot
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public DateOnly SnapshotDate { get; set; }

    /// <summary>Total quantity physically present in AWD distribution centers.</summary>
    public int TotalOnhandQuantity { get; set; }

    /// <summary>In-transit from the seller to AWD, not yet received.</summary>
    public int TotalInboundQuantity { get; set; }

    /// <summary>Available for downstream replenishment to FBA (or other channels).</summary>
    public int AvailableDistributableQuantity { get; set; }

    /// <summary>Reserved for replenishment orders already being prepared.</summary>
    public int ReservedDistributableQuantity { get; set; }

    /// <summary>In-transit from AWD to FBA.</summary>
    public int ReplenishmentQuantity { get; set; }

    public DateTimeOffset SyncedAt { get; set; }
}
