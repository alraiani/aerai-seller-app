namespace AERai.Web.Domain.Core;

/// <summary>
/// Units of a SKU held in Amazon Warehousing and Distribution (AWD) on a given date. Unique per
/// (marketplace, SKU, date).
/// </summary>
/// <remarks>
/// Kept apart from <see cref="InventorySnapshot"/> because AWD reports a fixed set of named
/// quantities rather than fulfillment states, and AWD stock is upstream of the FBA network: it has
/// to be replenished into FBA before it can sell.
/// </remarks>
public sealed class AwdInventorySnapshot
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>The date the quantities were read.</summary>
    public DateOnly SnapshotDate { get; set; }

    /// <summary>Marketplace whose schedule pulled the stock (AWD itself is regional).</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>Units physically in AWD distribution centers.</summary>
    public int OnHand { get; set; }

    /// <summary>Units on their way from the seller to AWD, not yet received.</summary>
    public int Inbound { get; set; }

    /// <summary>On-hand units free to be replenished into FBA.</summary>
    public int AvailableDistributable { get; set; }

    /// <summary>On-hand units set aside for replenishment orders already being prepared.</summary>
    public int ReservedDistributable { get; set; }

    /// <summary>Units in transit from AWD to FBA.</summary>
    public int Replenishment { get; set; }

    /// <summary>When the snapshot was pulled from Amazon.</summary>
    public DateTimeOffset SyncedAt { get; set; }
}
