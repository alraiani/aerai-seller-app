using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>How the changes from a count sheet are written to the ledger.</summary>
/// <param name="IncreaseType">Movement type for SKUs going up (Received from supplier, Count correction, or Other).</param>
/// <param name="DecreaseType">Movement type for SKUs going down (Shipped to Amazon, Count correction, or Other).</param>
/// <param name="Reference">Outside reference applied to every entry (PO, FBA shipment id).</param>
/// <param name="Note">Note applied to every entry (required when either type is Other).</param>
/// <param name="OccurredAt">When the units moved; <see langword="null"/> means now.</param>
public sealed record HomeStockCountOptions(
    HomeStockMovementType IncreaseType,
    HomeStockMovementType DecreaseType,
    string? Reference,
    string? Note,
    DateTimeOffset? OccurredAt)
{
    /// <summary>Types allowed for increases.</summary>
    public static IReadOnlyList<HomeStockMovementType> IncreaseTypes { get; } =
        [HomeStockMovementType.ReceivedFromSupplier, HomeStockMovementType.CountCorrection, HomeStockMovementType.Other];

    /// <summary>Types allowed for decreases.</summary>
    public static IReadOnlyList<HomeStockMovementType> DecreaseTypes { get; } =
        [HomeStockMovementType.ShippedToAmazon, HomeStockMovementType.CountCorrection, HomeStockMovementType.Other];
}
