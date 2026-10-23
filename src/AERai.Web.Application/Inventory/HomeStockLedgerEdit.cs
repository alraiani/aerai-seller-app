using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>What a listed entry was before it was last edited on the ledger page, and who edited it.</summary>
/// <param name="EditedAt">When the edit was made.</param>
/// <param name="EditedBy">Who made it.</param>
/// <param name="WasType">The kind of movement before the edit.</param>
/// <param name="WasUnits">The units before the edit.</param>
/// <param name="WasNote">The note before the edit.</param>
public sealed record HomeStockLedgerEdit(DateTimeOffset EditedAt, string EditedBy, HomeStockMovementType WasType, int WasUnits, string? WasNote);
