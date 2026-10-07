namespace AERai.Web.Domain.Reporting;

/// <summary>
/// Read model for <c>rpt.vw_InventoryPosition</c>: each SKU's latest snapshot pivoted by state,
/// combined with trailing 30-day sales velocity.
/// </summary>
public sealed class InventoryPosition
{
    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>Marketplace the position is for.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Product title, when known.</summary>
    public string? Title { get; set; }

    /// <summary>Date of the most recent snapshot for the SKU.</summary>
    public DateOnly SnapshotDate { get; set; }

    /// <summary>Units sellable now.</summary>
    public int Available { get; set; }

    /// <summary>Units on the way to the fulfillment network.</summary>
    public int Inbound { get; set; }

    /// <summary>Units reserved.</summary>
    public int Reserved { get; set; }

    /// <summary>Units not sellable.</summary>
    public int Unfulfillable { get; set; }

    /// <summary>Units sold over the trailing 30 days (excluding cancelled orders).</summary>
    public int UnitsSold30d { get; set; }

    /// <summary>Average units sold per day over the trailing 30 days.</summary>
    public decimal DailyVelocity { get; set; }

    /// <summary>
    /// (Available + Inbound) ÷ daily velocity; <see langword="null"/> when there were no sales in the window.
    /// </summary>
    public decimal? DaysOfSupply { get; set; }
}
