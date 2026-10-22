using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>
/// One kind of home-stock movement in the ledger's grouped view: totals over every matching entry,
/// and the newest of those entries.
/// </summary>
/// <param name="Type">The kind of movement.</param>
/// <param name="EntryCount">Matching entries of this kind.</param>
/// <param name="SkuCount">Distinct SKUs among them.</param>
/// <param name="UnitsIn">Units added (the sum of the positive entries).</param>
/// <param name="UnitsOut">Units removed (the sum of the negative entries, so zero or negative).</param>
/// <param name="LatestAt">When the most recent of them happened.</param>
/// <param name="Entries">The newest entries, newest logged first, at most <see cref="HomeStockLedgerService.MaxGroupRows"/>.</param>
public sealed record HomeStockLedgerGroup(
    HomeStockMovementType Type,
    int EntryCount,
    int SkuCount,
    int UnitsIn,
    int UnitsOut,
    DateTimeOffset LatestAt,
    IReadOnlyList<HomeStockLedgerEntry> Entries)
{
    /// <summary>Net change to home stock.</summary>
    public int NetUnits => UnitsIn + UnitsOut;

    /// <summary>Whether some entries were left out of <see cref="Entries"/>.</summary>
    public bool IsCapped => Entries.Count < EntryCount;
}
