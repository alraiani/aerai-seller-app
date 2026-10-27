using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>
/// One action in the ledger's grouped view: the entries one save wrote (a count sheet, a send to Amazon, a
/// worksheet save, a single logged movement, or an undo), which share when they were logged and by whom.
/// </summary>
/// <remarks>
/// Every save stamps all its entries with one logged time, so <see cref="LoggedAt"/> and <see cref="LoggedBy"/>
/// identify the action without a column of their own. Sends to Amazon are kept apart from the rest of the same save
/// (a Worksheet save can recount and send), so undoing a shipment never undoes the counts. An edit's corrected copy
/// keeps the original's logged time, so it stays in the action it belongs to.
/// </remarks>
/// <param name="LoggedAt">When the action was saved.</param>
/// <param name="LoggedBy">Who saved it.</param>
/// <param name="Types">The kinds of movement in it, in a fixed order (a count sheet can log two).</param>
/// <param name="EntryCount">Matching entries in it.</param>
/// <param name="SkuCount">Distinct SKUs among them.</param>
/// <param name="UnitsIn">Units added (the sum of the positive entries).</param>
/// <param name="UnitsOut">Units removed (the sum of the negative entries, so zero or negative).</param>
/// <param name="ReversalCount">How many of its entries are reversals of others.</param>
/// <param name="OccurredFrom">Earliest date the units moved.</param>
/// <param name="OccurredTo">Latest date the units moved.</param>
/// <param name="Reference">Its reference (a PO or FBA shipment id), when its entries have one.</param>
/// <param name="Entries">Its entries, newest logged first, at most <see cref="HomeStockLedgerService.MaxGroupRows"/>.</param>
public sealed record HomeStockLedgerAction(
    DateTimeOffset LoggedAt,
    string LoggedBy,
    IReadOnlyList<HomeStockMovementType> Types,
    int EntryCount,
    int SkuCount,
    int UnitsIn,
    int UnitsOut,
    int ReversalCount,
    DateTimeOffset OccurredFrom,
    DateTimeOffset OccurredTo,
    string? Reference,
    IReadOnlyList<HomeStockLedgerEntry> Entries)
{
    /// <summary>Net change to home stock.</summary>
    public int NetUnits => UnitsIn + UnitsOut;

    /// <summary>Whether some entries were left out of <see cref="Entries"/>.</summary>
    public bool IsCapped => Entries.Count < EntryCount;

    /// <summary>Whether the action undid other entries (every entry in it is a reversal).</summary>
    public bool IsUndo => EntryCount > 0 && ReversalCount == EntryCount;

    /// <summary>Whether some of its entries can still be reversed, so the whole action can be undone.</summary>
    public bool CanUndo => Entries.Any(e => e.CanReverse);

    /// <summary>Whether it was undone: nothing in it can be reversed any more and some of it was reversed.</summary>
    public bool IsUndone => !IsUndo && !CanUndo && Entries.Any(e => e.ReversedById is not null);

    /// <summary>An entry of the action, used to name it in links and posts (the newest one listed).</summary>
    public long AnchorId => Entries.Count > 0 ? Entries[0].Id : 0;
}
