namespace AERai.Web.Domain.Core;

/// <summary>
/// Canonical inventory state names used in <c>core.InventorySnapshot</c> and the reporting views.
/// </summary>
/// <remarks>
/// Kept as string constants (not an enum) because they are matched in SQL by the promotion
/// procedure and pivoted by <c>rpt.vw_InventoryPosition</c>; the values must stay in sync with that SQL.
/// </remarks>
public static class InventoryStates
{
    /// <summary>Sellable now.</summary>
    public const string Available = "Available";

    /// <summary>Shipped to the fulfillment network but not yet received.</summary>
    public const string Inbound = "Inbound";

    /// <summary>Held for pending orders, transfers, or processing.</summary>
    public const string Reserved = "Reserved";

    /// <summary>Damaged or otherwise not sellable.</summary>
    public const string Unfulfillable = "Unfulfillable";

    /// <summary>All recognized states, in display order.</summary>
    public static IReadOnlyList<string> All { get; } = [Available, Inbound, Reserved, Unfulfillable];
}
