namespace AERai.Web.Application.Inventory;

/// <summary>
/// The supply-chain timings used to plan a SKU's restock, with defaults already applied.
/// </summary>
/// <param name="SupplierLeadTimeDays">Days from ordering to the goods arriving.</param>
/// <param name="PrepTimeDays">Days to prepare goods for shipping.</param>
/// <param name="TransitDays">Days from shipping to Amazon until sellable.</param>
/// <param name="SafetyStockDays">Buffer kept before the projected stockout.</param>
/// <param name="TargetStockDays">Days of sales a restock should cover.</param>
/// <param name="IsCustom">Whether any value comes from the SKU's own profile rather than the defaults.</param>
public sealed record LeadTimes(int SupplierLeadTimeDays, int PrepTimeDays, int TransitDays, int SafetyStockDays, int TargetStockDays, bool IsCustom)
{
    /// <summary>Days from shipping home stock until it is sellable, plus the safety buffer.</summary>
    public int SendLeadDays => TransitDays + SafetyStockDays;

    /// <summary>Days from placing a supplier order until the units are sellable, plus the safety buffer.</summary>
    public int OrderLeadDays => SupplierLeadTimeDays + PrepTimeDays + SendLeadDays;
}
