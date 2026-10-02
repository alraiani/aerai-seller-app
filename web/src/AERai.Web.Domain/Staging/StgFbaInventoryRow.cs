namespace AERai.Web.Domain.Staging;

/// <summary>
/// A raw row of Amazon's FBA "Manage Inventory" report (<c>GET_FBA_MYI_ALL_INVENTORY_DATA</c>),
/// mirroring its wide layout: one row per SKU, one column per quantity state. The snapshot date is
/// the batch's receive date (the report itself carries none).
/// </summary>
public sealed class StgFbaInventoryRow : StagingRow
{
    /// <summary><c>sku</c>.</summary>
    public string? Sku { get; set; }

    /// <summary><c>asin</c>.</summary>
    public string? Asin { get; set; }

    /// <summary><c>product-name</c>.</summary>
    public string? ProductName { get; set; }

    /// <summary><c>afn-fulfillable-quantity</c> (sellable now).</summary>
    public string? AfnFulfillableQuantity { get; set; }

    /// <summary><c>afn-unsellable-quantity</c>.</summary>
    public string? AfnUnsellableQuantity { get; set; }

    /// <summary><c>afn-reserved-quantity</c>.</summary>
    public string? AfnReservedQuantity { get; set; }

    /// <summary><c>afn-inbound-working-quantity</c>.</summary>
    public string? AfnInboundWorkingQuantity { get; set; }

    /// <summary><c>afn-inbound-shipped-quantity</c>.</summary>
    public string? AfnInboundShippedQuantity { get; set; }

    /// <summary><c>afn-inbound-receiving-quantity</c>.</summary>
    public string? AfnInboundReceivingQuantity { get; set; }
}
