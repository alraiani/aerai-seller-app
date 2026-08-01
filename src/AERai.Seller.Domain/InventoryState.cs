namespace AERai.Seller.Domain;

/// <summary>
/// Amazon-side states come from the FBA Inventory Ledger/Planning reports.
/// The three "OnOrderFromSupplier"/"InPrep"/"ReadyToShipToFba" states are tracked
/// internally since Amazon has no visibility into the pre-FBA supply chain.
/// </summary>
public enum InventoryState
{
    Available,
    Inbound,
    Reserved,
    Unfulfillable,
    Researching,
    FcTransfer,
    FcProcessing,
    OnOrderFromSupplier,
    InPrep,
    ReadyToShipToFba,
}
