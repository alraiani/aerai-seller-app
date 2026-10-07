using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Persists the home-stock ledger. Every write records an entry and updates the SKU's balance
/// (<c>core.HomeStock</c>) in one transaction, so a SKU's entries always add up to its home stock.
/// </summary>
public interface IHomeStockLedgerRepository
{
    /// <summary>Records one entry and applies it to the balance.</summary>
    /// <param name="write">The entry.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What happened, and the new entry's id when it was recorded.</returns>
    Task<(HomeStockLedgerOutcome Outcome, long? Id)> RecordAsync(HomeStockLedgerWrite write, CancellationToken cancellationToken);

    /// <summary>Records a reversal of an entry: the same type, opposite units, linked to the original.</summary>
    /// <param name="marketplaceId">Marketplace of the entry.</param>
    /// <param name="id">The entry to reverse.</param>
    /// <param name="createdAt">When the reversal is logged.</param>
    /// <param name="createdBy">Email of the user reversing it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What happened, and the reversal's id when it was recorded.</returns>
    Task<(HomeStockLedgerOutcome Outcome, long? Id)> ReverseAsync(string marketplaceId, long id, DateTimeOffset createdAt, string createdBy, CancellationToken cancellationToken);

    /// <summary>
    /// Applies reviewed count-sheet changes in one transaction: for each SKU whose home stock still
    /// equals the reviewed <see cref="HomeStockCountChange.Current"/>, records the difference with
    /// the type for its direction; any other SKU is skipped as stale.
    /// </summary>
    /// <param name="marketplaceId">Marketplace the stock is held for.</param>
    /// <param name="changes">The reviewed changes.</param>
    /// <param name="increaseType">Movement type for increases.</param>
    /// <param name="decreaseType">Movement type for decreases.</param>
    /// <param name="template">Reference, note, dates, and user applied to every entry (its SKU, type, and units are ignored).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The changes applied, and the SKUs skipped as stale or unknown.</returns>
    Task<(IReadOnlyList<HomeStockCountChange> Applied, IReadOnlyList<string> Stale)> ApplyCountsAsync(
        string marketplaceId,
        IReadOnlyList<HomeStockCountChange> changes,
        HomeStockMovementType increaseType,
        HomeStockMovementType decreaseType,
        HomeStockLedgerWrite template,
        CancellationToken cancellationToken);

    /// <summary>One page of a marketplace's entries, newest first.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="filter">SKU, family, type, and date range.</param>
    /// <param name="request">Paging; search matches SKU, title, reference, or note.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page.</returns>
    Task<PagedResult<HomeStockLedgerEntry>> ListAsync(string marketplaceId, HomeStockLedgerFilter filter, PageRequest request, CancellationToken cancellationToken);
}
