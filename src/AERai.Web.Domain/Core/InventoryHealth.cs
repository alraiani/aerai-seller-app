namespace AERai.Web.Domain.Core;

/// <summary>
/// Amazon's latest view of a SKU's stock age and excess in one marketplace, from the FBA inventory planning report:
/// what is old enough to draw the aged-inventory surcharge and what Amazon expects not to sell in time. Written only
/// by promotion.
/// </summary>
/// <remarks>
/// Only the latest report is kept: each one is a complete snapshot, so promoting a new report replaces the
/// marketplace's previous rows.
/// </remarks>
public sealed class InventoryHealth
{
    /// <summary>Marketplace.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>UTC date the report was received.</summary>
    public DateOnly SnapshotDate { get; set; }

    /// <summary>Units in stock up to 90 days.</summary>
    public int Age0To90 { get; set; }

    /// <summary>Units in stock 91–180 days.</summary>
    public int Age91To180 { get; set; }

    /// <summary>Units in stock 181–270 days (the aged-inventory surcharge starts at 181).</summary>
    public int Age181To270 { get; set; }

    /// <summary>Units in stock 271–365 days.</summary>
    public int Age271To365 { get; set; }

    /// <summary>Units in stock over a year.</summary>
    public int Age365Plus { get; set; }

    /// <summary>Units Amazon estimates are more than will sell in time (0 = none).</summary>
    public int EstimatedExcess { get; set; }

    /// <summary>Amazon's estimate of how many days the stock lasts, when it gives one.</summary>
    public int? DaysOfSupply { get; set; }

    /// <summary>Monthly storage fee Amazon estimates for next month, in <see cref="Currency"/>.</summary>
    public decimal? StorageCostNextMonth { get; set; }

    /// <summary>Aged-inventory surcharge Amazon estimates for the next charge, in <see cref="Currency"/>.</summary>
    public decimal? AgedSurchargeNextMonth { get; set; }

    /// <summary>Currency of the fees.</summary>
    public string? Currency { get; set; }

    /// <summary>The import batch that last wrote this row.</summary>
    public long LastImportBatchId { get; set; }
}
