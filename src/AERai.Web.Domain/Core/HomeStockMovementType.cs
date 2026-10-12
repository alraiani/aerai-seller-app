namespace AERai.Web.Domain.Core;

/// <summary>Why a SKU's home stock changed (see <see cref="HomeStockMovement"/>).</summary>
public enum HomeStockMovementType
{
    /// <summary>The balance carried over when the ledger started; one per SKU and marketplace.</summary>
    OpeningBalance = 1,

    /// <summary>An incoming shipment from the supplier (adds units).</summary>
    ReceivedFromSupplier = 2,

    /// <summary>An outgoing shipment into Amazon's fulfillment network (removes units).</summary>
    ShippedToAmazon = 3,

    /// <summary>A recount that sets the balance to what is actually on hand (either sign).</summary>
    CountCorrection = 4,

    /// <summary>Anything else (damaged, gifted, returned from Amazon, …); needs a note (either sign).</summary>
    Other = 5,
}
