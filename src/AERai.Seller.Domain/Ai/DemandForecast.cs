namespace AERai.Seller.Domain.Ai;

/// <summary>
/// A point-in-time demand projection for a SKU, computed from staged order/inventory history.
/// Historized (one row per computation) rather than a single current-state row, the same way
/// <see cref="Staging.InventorySnapshot"/> is, so past forecasts can be compared against what
/// actually happened.
/// </summary>
public class DemandForecast
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public DateTimeOffset ComputedAt { get; set; }

    /// <summary>Rolling-weighted average units sold per day at computation time.</summary>
    public decimal DailySalesVelocity { get; set; }

    /// <summary>
    /// FBA (Available + Inbound + FcTransfer + FcProcessing) plus AWD (AvailableDistributableQuantity
    /// + ReplenishmentQuantity) at computation time. AWD's TotalInboundQuantity (seller→AWD, not yet
    /// received) is excluded, same as FBA's OnOrderFromSupplier is not counted.
    /// </summary>
    public int SellThroughEligibleStock { get; set; }

    public decimal DaysOfSupply { get; set; }
    public DateOnly? ProjectedStockoutDate { get; set; }
}
