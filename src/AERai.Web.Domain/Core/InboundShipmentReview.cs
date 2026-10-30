namespace AERai.Web.Domain.Core;

/// <summary>Where an <see cref="InboundShipment"/> stands on its way to the home-stock ledger.</summary>
public enum InboundShipmentReview
{
    /// <summary>Waiting for an Operator to accept (log it as a send) or dismiss it.</summary>
    ToReview = 1,

    /// <summary>Accepted: logged as a Shipped to Amazon send carrying the shipment's FBA id.</summary>
    Logged = 2,

    /// <summary>Set aside without touching home stock (it didn't come from home, or was logged some other way).</summary>
    Dismissed = 3,

    /// <summary>A send with the shipment's FBA id was already in the ledger when the sync first saw it.</summary>
    AlreadyLogged = 4,

    /// <summary>Amazon cancelled or deleted the shipment before anyone accepted it.</summary>
    CancelledByAmazon = 5,
}
