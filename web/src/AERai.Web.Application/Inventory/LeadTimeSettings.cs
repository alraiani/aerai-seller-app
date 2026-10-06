namespace AERai.Web.Application.Inventory;

/// <summary>A SKU's own lead-time overrides as entered; a null field uses the app-wide default.</summary>
/// <param name="SupplierLeadTimeDays">Days from ordering to the goods arriving.</param>
/// <param name="PrepTimeDays">Days to prepare goods for shipping.</param>
/// <param name="TransitDays">Days from shipping to Amazon until sellable.</param>
/// <param name="SafetyStockDays">Buffer kept before the projected stockout.</param>
/// <param name="TargetStockDays">Days of sales a restock should cover.</param>
public sealed record LeadTimeSettings(int? SupplierLeadTimeDays, int? PrepTimeDays, int? TransitDays, int? SafetyStockDays, int? TargetStockDays)
{
    /// <summary>No overrides: every value uses the default.</summary>
    public static LeadTimeSettings None { get; } = new(null, null, null, null, null);

    /// <summary>Whether every field is blank.</summary>
    public bool IsEmpty => this == None;
}
