namespace AERai.Seller.Domain.Ai;

/// <summary>
/// Per-SKU configuration of the supply chain stages Amazon itself has no visibility into
/// (supplier → prep → FBA), entered by the user. Consumed by demand forecasting/replenishment
/// to work backward from a projected stockout date to actionable order/prep/ship dates.
/// </summary>
public class LeadTimeProfile
{
    public required string Sku { get; set; }

    /// <summary>Days from placing a supplier order to raw product arrival.</summary>
    public int SupplierLeadTimeDays { get; set; }

    /// <summary>Days from raw product arrival to ready-to-ship (prep/packaging).</summary>
    public int PrepTimeDays { get; set; }

    /// <summary>Days from shipping to FBA to the units becoming available for sale (transit + receiving).</summary>
    public int FbaTransitDays { get; set; }

    /// <summary>Extra buffer, in days of supply, held back from the projected stockout date.</summary>
    public int SafetyStockDays { get; set; }

    /// <summary>
    /// Target number of days of stock to keep on hand for this SKU, overriding the app-wide
    /// default target stock days setting when set. Drives how much (not just when) to reorder:
    /// recommended order quantity tops stock up to this many days of projected demand.
    /// </summary>
    public int? TargetStockDays { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
