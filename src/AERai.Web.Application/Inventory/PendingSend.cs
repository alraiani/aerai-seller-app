using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>
/// A send from home stock to Amazon (a <see cref="HomeStockMovementType.ShippedToAmazon"/> entry that hasn't
/// been reversed) and how much of it Amazon's numbers show so far.
/// </summary>
/// <param name="Id">The ledger entry's id.</param>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Title">Product title, when known.</param>
/// <param name="FamilyId">The product's family, if assigned.</param>
/// <param name="Color">The variant's color, if set.</param>
/// <param name="OccurredAt">When the units were sent (entered by the user).</param>
/// <param name="CreatedAt">When the send was logged; Amazon's numbers are compared from this point.</param>
/// <param name="CreatedBy">Email of the user who logged it.</param>
/// <param name="UnitsSent">Units sent (positive).</param>
/// <param name="ReconciledUnits">Units Amazon's numbers already show.</param>
/// <param name="AmazonBaseline">The SKU's FBA stock when the send was logged, or <see langword="null"/> for sends logged before it was recorded.</param>
/// <param name="Reference">Outside reference: the FBA shipment id, when given.</param>
/// <param name="IsOpen">Whether the send is still open (not fully matched or marked reconciled).</param>
public sealed record PendingSend(
    long Id,
    string Sku,
    string? Title,
    int? FamilyId,
    ProductColor? Color,
    DateTimeOffset OccurredAt,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    int UnitsSent,
    int ReconciledUnits,
    AmazonBaseline? AmazonBaseline,
    string? Reference,
    bool IsOpen)
{
    /// <summary>Units Amazon doesn't show yet (0 once closed).</summary>
    public int Pending => IsOpen ? Math.Max(0, UnitsSent - ReconciledUnits) : 0;

    /// <summary>
    /// Whether the reference is an FBA shipment id (Amazon's ids start with "FBA"). Other references, such
    /// as a purchase order typed on a count-sheet upload, don't identify a shipment.
    /// </summary>
    public bool HasShipmentId => IsShipmentId(Reference);

    /// <summary>Whether a reference looks like an FBA shipment id.</summary>
    /// <param name="reference">The reference.</param>
    /// <returns><see langword="true"/> when it starts with "FBA".</returns>
    public static bool IsShipmentId(string? reference) =>
        reference is { Length: > 3 } r && r.StartsWith("FBA", StringComparison.OrdinalIgnoreCase);
}
