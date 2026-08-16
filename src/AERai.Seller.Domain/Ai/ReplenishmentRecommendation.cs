namespace AERai.Seller.Domain.Ai;

/// <summary>
/// The computed replenishment action plan for a SKU at a point in time: how many units to move
/// through each pipeline stage and by when, worked backward from a <see cref="DemandForecast"/>'s
/// projected stockout date and the SKU's <see cref="LeadTimeProfile"/>. Historized like
/// <see cref="DemandForecast"/> so recommendations can be audited against what was actually done.
/// </summary>
public class ReplenishmentRecommendation
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public DateTimeOffset ComputedAt { get; set; }

    public int RecommendedOrderQuantity { get; set; }
    public DateOnly RecommendedOrderBy { get; set; }

    public int UnitsDueOutOfPrep { get; set; }
    public DateOnly PrepDueBy { get; set; }

    public int UnitsToShipToFba { get; set; }
    public DateOnly ShipToFbaBy { get; set; }

    /// <summary>Days until the next action is overdue — lets the dashboard sort by urgency.</summary>
    public int DaysUntilActionNeeded { get; set; }
}
