using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>How a worksheet save logs its home-stock changes and sends.</summary>
/// <param name="IncreaseType">Movement type for units added (one of <see cref="IncreaseTypes"/>).</param>
/// <param name="DecreaseType">Movement type for units removed (one of <see cref="DecreaseTypes"/>).</param>
/// <param name="Note">Note on every change (required when either type is Other).</param>
/// <param name="ShipmentReference">FBA shipment id on every send, if given.</param>
public sealed record HomeStockWorksheetOptions(
    HomeStockMovementType IncreaseType,
    HomeStockMovementType DecreaseType,
    string? Note,
    string? ShipmentReference)
{
    /// <summary>Types allowed for units added.</summary>
    public static IReadOnlyList<HomeStockMovementType> IncreaseTypes => HomeStockCountOptions.IncreaseTypes;

    /// <summary>
    /// Types allowed for units removed. Not Shipped to Amazon: the worksheet's send box logs those, with
    /// the shipment id, and a removal logged that way would sit as a pending send nobody entered.
    /// </summary>
    public static IReadOnlyList<HomeStockMovementType> DecreaseTypes { get; } =
        [.. HomeStockCountOptions.DecreaseTypes.Where(t => t != HomeStockMovementType.ShippedToAmazon)];
}
