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

    /// <summary>
    /// Records a reversal of an entry: the same type, opposite units, linked to the original. A send Amazon
    /// already shows part of can't be reversed (<see cref="HomeStockLedgerOutcome.AlreadyAtAmazon"/>).
    /// </summary>
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
    /// <param name="amazonBaselines">
    /// Each SKU's FBA stock now, stored on decreases logged as <see cref="HomeStockMovementType.ShippedToAmazon"/>
    /// (see <see cref="HomeStockMovement.AmazonBaselineUnits"/>); a SKU missing from it had nothing at Amazon.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The changes applied, and the SKUs skipped as stale or unknown.</returns>
    Task<(IReadOnlyList<HomeStockCountChange> Applied, IReadOnlyList<string> Stale)> ApplyCountsAsync(
        string marketplaceId,
        IReadOnlyList<HomeStockCountChange> changes,
        HomeStockMovementType increaseType,
        HomeStockMovementType decreaseType,
        HomeStockLedgerWrite template,
        IReadOnlyDictionary<string, AmazonBaseline> amazonBaselines,
        CancellationToken cancellationToken);

    /// <summary>
    /// Corrects an entry: reverses it and logs a copy with the new type, units, and note (linked by
    /// <see cref="HomeStockMovement.CorrectsId"/>), keeping its date, reference, who and when it was logged,
    /// and, for a send to Amazon, its baseline and the units Amazon already shows. One serializable
    /// transaction, so the balance and the entries stay equal.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="id">The entry.</param>
    /// <param name="type">The corrected kind of movement.</param>
    /// <param name="units">The corrected signed units.</param>
    /// <param name="note">The corrected note.</param>
    /// <param name="amazonBaselines">
    /// Each SKU's Amazon baseline now, used when an entry becomes a send to Amazon (it had none before).
    /// </param>
    /// <param name="createdAt">When the correction is made.</param>
    /// <param name="createdBy">Email of the user making it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The outcome and the corrected copy's id: <see cref="HomeStockLedgerOutcome.Unchanged"/> when nothing
    /// differs, <see cref="HomeStockLedgerOutcome.CannotReverse"/> for an entry that can't be edited,
    /// <see cref="HomeStockLedgerOutcome.WouldGoNegative"/>, or <see cref="HomeStockLedgerOutcome.AlreadyAtAmazon"/>.
    /// </returns>
    Task<(HomeStockLedgerOutcome Outcome, long? Id)> CorrectAsync(
        string marketplaceId,
        long id,
        HomeStockMovementType type,
        int units,
        string? note,
        IReadOnlyDictionary<string, AmazonBaseline> amazonBaselines,
        DateTimeOffset createdAt,
        string createdBy,
        CancellationToken cancellationToken);

    /// <summary>A marketplace's entries by id (any that aren't its own are left out).</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="ids">The entries.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The entries found.</returns>
    Task<IReadOnlyList<HomeStockLedgerEntry>> GetEntriesAsync(string marketplaceId, IReadOnlyCollection<long> ids, CancellationToken cancellationToken);

    /// <summary>One page of a marketplace's entries, newest first.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="filter">SKU, family, type, and date range.</param>
    /// <param name="request">Paging; search matches SKU, title, reference, or note.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page.</returns>
    Task<PagedResult<HomeStockLedgerEntry>> ListAsync(string marketplaceId, HomeStockLedgerFilter filter, PageRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// One page of a marketplace's actions (entries sharing when they were logged and by whom), newest first.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="filter">SKU, family, type, and date range (applied to the entries, so totals count matching ones).</param>
    /// <param name="request">Paging (in actions); search matches SKU, title, reference, or note.</param>
    /// <param name="maxRows">Most entries to return per action (the newest).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page of actions.</returns>
    Task<PagedResult<HomeStockLedgerAction>> ListActionsAsync(string marketplaceId, HomeStockLedgerFilter filter, PageRequest request, int maxRows, CancellationToken cancellationToken);

    /// <summary>
    /// Undoes the action an entry belongs to: reverses every entry logged with it that can still be reversed,
    /// all in one serializable transaction, every reversal logged at <paramref name="createdAt"/> (so the undo
    /// is one action too). Nothing is written unless all of them can be.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="entryId">Any entry of the action.</param>
    /// <param name="createdAt">When the undo is logged.</param>
    /// <param name="createdBy">Email of the user undoing it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The outcome, how many entries were reversed, and the SKUs that blocked it:
    /// <see cref="HomeStockLedgerOutcome.NotFound"/>, <see cref="HomeStockLedgerOutcome.CannotReverse"/> (nothing
    /// left to undo), <see cref="HomeStockLedgerOutcome.WouldGoNegative"/>, or
    /// <see cref="HomeStockLedgerOutcome.AlreadyAtAmazon"/> (a send Amazon already shows part of).
    /// </returns>
    Task<(HomeStockLedgerOutcome Outcome, int Reversed, IReadOnlyList<string> BlockingSkus)> ReverseActionAsync(
        string marketplaceId, long entryId, DateTimeOffset createdAt, string createdBy, CancellationToken cancellationToken);

    /// <summary>
    /// Reverses the given entries (those that can still be reversed), all or nothing, like
    /// <see cref="ReverseActionAsync"/>.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="ids">The entries (any that aren't the marketplace's own are ignored).</param>
    /// <param name="createdAt">When the reversals are logged.</param>
    /// <param name="createdBy">Email of the user reversing them.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The same as <see cref="ReverseActionAsync"/>; <see cref="HomeStockLedgerOutcome.CannotReverse"/> when none can be reversed.</returns>
    Task<(HomeStockLedgerOutcome Outcome, int Reversed, IReadOnlyList<string> BlockingSkus)> ReverseEntriesAsync(
        string marketplaceId, IReadOnlyCollection<long> ids, DateTimeOffset createdAt, string createdBy, CancellationToken cancellationToken);
}
