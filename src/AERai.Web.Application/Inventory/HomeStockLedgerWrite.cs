using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>A validated ledger entry ready to be recorded (see <see cref="IHomeStockLedgerService"/>).</summary>
/// <param name="MarketplaceId">Marketplace the stock is held for.</param>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Type">Kind of movement.</param>
/// <param name="Units">
/// Signed units to add or remove; for <see cref="HomeStockMovementType.CountCorrection"/>, the counted
/// total instead (the repository turns it into the difference from the current balance).
/// </param>
/// <param name="OccurredAt">When the units moved.</param>
/// <param name="Reference">Outside reference, if any.</param>
/// <param name="Note">Note, if any.</param>
/// <param name="ReversesId">The entry this one reverses, if any.</param>
/// <param name="CreatedAt">When it was logged.</param>
/// <param name="CreatedBy">Email of the user logging it.</param>
public sealed record HomeStockLedgerWrite(
    string MarketplaceId,
    string Sku,
    HomeStockMovementType Type,
    int Units,
    DateTimeOffset OccurredAt,
    string? Reference,
    string? Note,
    long? ReversesId,
    DateTimeOffset CreatedAt,
    string CreatedBy);
