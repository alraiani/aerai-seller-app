using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>A movement as a user enters it on the "Log movement" form.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Type">Kind of movement (not <see cref="HomeStockMovementType.OpeningBalance"/>).</param>
/// <param name="Quantity">
/// Units received or shipped (positive); the new counted total for a count correction; signed units
/// for <see cref="HomeStockMovementType.Other"/>.
/// </param>
/// <param name="OccurredAt">When it happened; <see langword="null"/> means now.</param>
/// <param name="Reference">Purchase order, FBA shipment id, etc.</param>
/// <param name="Note">Note (required for <see cref="HomeStockMovementType.Other"/>).</param>
public sealed record HomeStockMovementInput(
    string Sku,
    HomeStockMovementType Type,
    int Quantity,
    DateTimeOffset? OccurredAt,
    string? Reference,
    string? Note);
