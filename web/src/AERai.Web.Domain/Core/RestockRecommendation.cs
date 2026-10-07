namespace AERai.Web.Domain.Core;

/// <summary>
/// Amazon's latest recommendation for how many units of a SKU to send into one marketplace's
/// fulfillment network and by when, from the restock inventory report. Written only by promotion.
/// </summary>
/// <remarks>
/// Only the latest report is kept: each one is a complete snapshot of Amazon's current advice, so
/// promoting a new report replaces the marketplace's previous recommendations.
/// </remarks>
public sealed class RestockRecommendation
{
    /// <summary>Marketplace the recommendation is for.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>UTC date the report was received.</summary>
    public DateOnly SnapshotDate { get; set; }

    /// <summary>Units Amazon recommends sending (0 = none needed).</summary>
    public int RecommendedQuantity { get; set; }

    /// <summary>The date Amazon recommends shipping by, when it gives one.</summary>
    public DateOnly? RecommendedShipDate { get; set; }

    /// <summary>Amazon's suggested action, e.g. "Create shipping plan".</summary>
    public string? RecommendedAction { get; set; }

    /// <summary>The import batch that last wrote this row.</summary>
    public long LastImportBatchId { get; set; }
}
