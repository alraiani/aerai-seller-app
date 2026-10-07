namespace AERai.Web.Domain.Staging;

/// <summary>
/// The kind of raw report a staging batch was imported from. Determines which staging table
/// receives the rows and which promotion branch moves them into <c>core</c>.
/// </summary>
public enum ImportSource
{
    /// <summary>Flat order report (one row per order line), e.g. Amazon's "All Orders" report.</summary>
    Orders = 1,

    /// <summary>Per-SKU, per-state inventory quantities on a snapshot date.</summary>
    Inventory = 2,

    /// <summary>Settlement flat file V2 (<c>GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE_V2</c>): one row per settlement amount.</summary>
    Settlements = 3,

    /// <summary>
    /// Amazon's native FBA inventory report (<c>GET_FBA_MYI_ALL_INVENTORY_DATA</c>): one wide row per
    /// SKU with a column per quantity state. Promotion unpivots it into per-state snapshots.
    /// </summary>
    FbaInventory = 4,
}
