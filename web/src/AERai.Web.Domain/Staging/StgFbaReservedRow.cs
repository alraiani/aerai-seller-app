namespace AERai.Web.Domain.Staging;

/// <summary>
/// A raw row of Amazon's reserved inventory report (<c>GET_RESERVED_INVENTORY_DATA</c>): one row per
/// SKU with reserved stock split by reason. The snapshot date is the batch's receive date (the
/// report itself carries none), matching <see cref="StgFbaInventoryRow"/>.
/// </summary>
public sealed class StgFbaReservedRow : StagingRow
{
    /// <summary><c>sku</c>.</summary>
    public string? Sku { get; set; }

    /// <summary><c>asin</c>.</summary>
    public string? Asin { get; set; }

    /// <summary><c>product-name</c>.</summary>
    public string? ProductName { get; set; }

    /// <summary><c>reserved_qty</c> (the total of the three reasons).</summary>
    public string? ReservedQuantity { get; set; }

    /// <summary><c>reserved_customerorders</c>.</summary>
    public string? ReservedCustomerOrders { get; set; }

    /// <summary><c>reserved_fc-transfers</c>.</summary>
    public string? ReservedFcTransfers { get; set; }

    /// <summary><c>reserved_fc-processing</c>.</summary>
    public string? ReservedFcProcessing { get; set; }
}
