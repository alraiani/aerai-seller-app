namespace AERai.Web.Domain.Staging;

/// <summary>
/// A raw row of Amazon's restock inventory report (<c>GET_RESTOCK_INVENTORY_RECOMMENDATIONS_REPORT</c>):
/// one row per SKU with Amazon's replenishment recommendation. Only the columns restock planning
/// uses are staged; the full line is kept in <see cref="StagingRow.RawLine"/>.
/// </summary>
public sealed class StgFbaRestockRow : StagingRow
{
    /// <summary><c>Merchant SKU</c>.</summary>
    public string? Sku { get; set; }

    /// <summary><c>ASIN</c>.</summary>
    public string? Asin { get; set; }

    /// <summary><c>Product Name</c>.</summary>
    public string? ProductName { get; set; }

    /// <summary><c>Recommended replenishment qty</c>.</summary>
    public string? RecommendedQuantity { get; set; }

    /// <summary><c>Recommended ship date</c> (blank when Amazon has no recommendation).</summary>
    public string? RecommendedShipDate { get; set; }

    /// <summary><c>Recommended action</c>, e.g. "Create shipping plan".</summary>
    public string? RecommendedAction { get; set; }
}
