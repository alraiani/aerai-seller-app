namespace AERai.Web.Domain.Core;

/// <summary>
/// Canonical inventory state names used in <c>core.InventorySnapshot</c> and the reporting views.
/// </summary>
/// <remarks>
/// Kept as string constants (not an enum) because they are matched in SQL by the promotion
/// procedure and pivoted by <c>rpt.vw_InventoryPosition</c>; the values must stay in sync with that SQL.
/// Amazon's reports give inbound and reserved stock in stages, which are stored separately. The
/// unsplit <see cref="Inbound"/> and <see cref="Reserved"/> states remain for sources that only
/// know the total (the generic upload file, or a day with no reserved-inventory report).
/// </remarks>
public static class InventoryStates
{
    /// <summary>Sellable now.</summary>
    public const string Available = "Available";

    /// <summary>On an inbound shipment plan that has not shipped yet.</summary>
    public const string InboundWorking = "InboundWorking";

    /// <summary>Shipped to the fulfillment network, in transit.</summary>
    public const string InboundShipped = "InboundShipped";

    /// <summary>Arrived at a fulfillment center and being received.</summary>
    public const string InboundReceiving = "InboundReceiving";

    /// <summary>Inbound with no stage breakdown.</summary>
    public const string Inbound = "Inbound";

    /// <summary>Reserved for customer orders that have not shipped yet (already sold).</summary>
    public const string ReservedCustomerOrder = "ReservedCustomerOrder";

    /// <summary>Being moved between fulfillment centers.</summary>
    public const string ReservedFcTransfer = "ReservedFcTransfer";

    /// <summary>Held at a fulfillment center for processing (e.g. measuring, investigation).</summary>
    public const string ReservedFcProcessing = "ReservedFcProcessing";

    /// <summary>Reserved with no breakdown.</summary>
    public const string Reserved = "Reserved";

    /// <summary>Damaged or otherwise not sellable.</summary>
    public const string Unfulfillable = "Unfulfillable";

    /// <summary>All recognized states, in display order.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Available,
        InboundWorking, InboundShipped, InboundReceiving, Inbound,
        ReservedCustomerOrder, ReservedFcTransfer, ReservedFcProcessing, Reserved,
        Unfulfillable,
    ];
}
