namespace AERai.Web.Application.Inventory;

/// <summary>Which open sends a pending-sends list shows.</summary>
public enum PendingSendView
{
    /// <summary>Every open send.</summary>
    All = 0,

    /// <summary>Sends logged without an FBA shipment id (matched only by the delta rule until one is added).</summary>
    NeedsShipmentId = 1,

    /// <summary>Sends Amazon still doesn't show after <see cref="InventoryOptions.PendingSendExpiryDays"/>.</summary>
    Overdue = 2,
}
