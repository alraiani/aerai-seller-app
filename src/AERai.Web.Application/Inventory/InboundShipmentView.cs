namespace AERai.Web.Application.Inventory;

/// <summary>Which Amazon shipments the review page lists.</summary>
public enum InboundShipmentView
{
    /// <summary>Shipments with SKUs from home stock, waiting to be accepted or dismissed.</summary>
    ToReview = 0,

    /// <summary>Shipments logged as sends (accepted here, or already in the ledger when first seen).</summary>
    Logged = 1,

    /// <summary>Shipments set aside: dismissed, cancelled by Amazon, or with no SKUs from home stock.</summary>
    SetAside = 2,

    /// <summary>Every shipment.</summary>
    All = 3,
}
