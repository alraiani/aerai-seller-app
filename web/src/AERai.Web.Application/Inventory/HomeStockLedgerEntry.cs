using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>One ledger line as shown on the ledger page.</summary>
/// <param name="Id">Entry id.</param>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Title">Product title, when known.</param>
/// <param name="Color">The SKU's color, if set.</param>
/// <param name="OccurredAt">When the units moved.</param>
/// <param name="Type">Kind of movement.</param>
/// <param name="Units">Units added (positive) or removed (negative).</param>
/// <param name="BalanceAfter">The SKU's home stock after this entry.</param>
/// <param name="Reference">Outside reference.</param>
/// <param name="Note">Note.</param>
/// <param name="ReversesId">The entry this one reverses, if any.</param>
/// <param name="ReversedById">The entry that reversed this one, if any.</param>
/// <param name="CreatedAt">When it was logged.</param>
/// <param name="CreatedBy">Who logged it.</param>
public sealed record HomeStockLedgerEntry(
    long Id,
    string Sku,
    string? Title,
    ProductColor? Color,
    DateTimeOffset OccurredAt,
    HomeStockMovementType Type,
    int Units,
    int BalanceAfter,
    string? Reference,
    string? Note,
    long? ReversesId,
    long? ReversedById,
    DateTimeOffset CreatedAt,
    string CreatedBy)
{
    /// <summary>Whether this entry can still be reversed (not a reversal, not already reversed, not the opening balance).</summary>
    public bool CanReverse => ReversesId is null && ReversedById is null && Type != HomeStockMovementType.OpeningBalance;
}
