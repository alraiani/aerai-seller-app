namespace AERai.Web.Domain.Staging;

/// <summary>
/// A raw row of Amazon's FBA inventory planning report (<c>GET_FBA_INVENTORY_PLANNING_DATA</c>): one row per SKU
/// with its stock by age, Amazon's estimated excess, and the fees coming. Only the columns restock planning uses
/// are staged; the full line is kept in <see cref="StagingRow.RawLine"/>.
/// </summary>
public sealed class StgFbaPlanningRow : StagingRow
{
    /// <summary><c>sku</c>.</summary>
    public string? Sku { get; set; }

    /// <summary><c>asin</c>.</summary>
    public string? Asin { get; set; }

    /// <summary><c>product-name</c>.</summary>
    public string? ProductName { get; set; }

    /// <summary><c>marketplace</c> (e.g. <c>US</c>); blank means the batch's own.</summary>
    public string? Marketplace { get; set; }

    /// <summary><c>inv-age-0-to-90-days</c>.</summary>
    public string? Age0To90 { get; set; }

    /// <summary><c>inv-age-91-to-180-days</c>.</summary>
    public string? Age91To180 { get; set; }

    /// <summary><c>inv-age-181-to-270-days</c>.</summary>
    public string? Age181To270 { get; set; }

    /// <summary><c>inv-age-271-to-365-days</c>.</summary>
    public string? Age271To365 { get; set; }

    /// <summary><c>inv-age-365-plus-days</c>.</summary>
    public string? Age365Plus { get; set; }

    /// <summary><c>estimated-excess-quantity</c>: units Amazon thinks are more than will sell in time.</summary>
    public string? EstimatedExcess { get; set; }

    /// <summary><c>days-of-supply</c>: Amazon's estimate of how long the stock lasts.</summary>
    public string? DaysOfSupply { get; set; }

    /// <summary><c>estimated-storage-cost-next-month</c>.</summary>
    public string? StorageCostNextMonth { get; set; }

    /// <summary>
    /// The aged-inventory surcharge Amazon estimates for the next charge: the sum of its <c>estimated-ais-*</c>
    /// columns (one per age band), added up when the row is staged.
    /// </summary>
    public string? AgedSurchargeNextMonth { get; set; }

    /// <summary><c>currency</c> of the fee columns.</summary>
    public string? Currency { get; set; }
}
