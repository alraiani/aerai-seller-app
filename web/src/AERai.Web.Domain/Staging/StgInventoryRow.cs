namespace AERai.Web.Domain.Staging;

/// <summary>
/// A raw inventory row: the quantity of one SKU in one fulfillment state on a snapshot date.
/// </summary>
public sealed class StgInventoryRow : StagingRow
{
    /// <summary><c>snapshot-date</c> (ISO 8601 date text).</summary>
    public string? SnapshotDate { get; set; }

    /// <summary><c>sku</c>.</summary>
    public string? Sku { get; set; }

    /// <summary><c>asin</c>.</summary>
    public string? Asin { get; set; }

    /// <summary><c>product-name</c>.</summary>
    public string? ProductName { get; set; }

    /// <summary><c>state</c>, e.g. Available, Inbound, Reserved, Unfulfillable.</summary>
    public string? State { get; set; }

    /// <summary><c>quantity</c>.</summary>
    public string? Quantity { get; set; }
}
