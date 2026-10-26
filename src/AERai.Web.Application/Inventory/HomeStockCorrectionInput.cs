using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>An edit to a ledger entry as a user makes it on the ledger page (recorded as a correction).</summary>
/// <param name="Id">The entry being edited.</param>
/// <param name="Type">Its kind of movement (not <see cref="HomeStockMovementType.OpeningBalance"/>).</param>
/// <param name="Units">
/// Its units. Received is always added and shipped always removed, whatever sign is typed; a count
/// correction or other movement keeps the sign typed (+ adds, − removes).
/// </param>
/// <param name="Note">Its note (required for <see cref="HomeStockMovementType.Other"/>); blank clears it.</param>
public sealed record HomeStockCorrectionInput(long Id, HomeStockMovementType Type, int Units, string? Note);
