using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>
/// Use cases for the home-stock ledger: an accounting-style log of every unit that comes into or
/// goes out of home stock. Entries are never edited or deleted; mistakes are undone by reversing.
/// </summary>
public interface IHomeStockLedgerService
{
    /// <summary>Validates and records a movement a user entered.</summary>
    /// <param name="marketplaceId">Marketplace the stock is held for.</param>
    /// <param name="input">The movement.</param>
    /// <param name="user">Email of the user logging it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A short description of what was recorded, or why it was not.</returns>
    Task<Result<string>> RecordAsync(string marketplaceId, HomeStockMovementInput input, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a reviewed count sheet: one ledger entry per changed SKU, increases and decreases
    /// logged with the chosen types. SKUs whose home stock changed since the review are skipped.
    /// </summary>
    /// <param name="marketplaceId">Marketplace the stock is held for.</param>
    /// <param name="changes">The reviewed changes (see <see cref="HomeStockCountPayload"/>).</param>
    /// <param name="options">Movement types, reference, note, and date.</param>
    /// <param name="user">Email of the user applying it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What was applied and skipped, or why nothing was.</returns>
    Task<Result<HomeStockCountResult>> ApplyCountsAsync(string marketplaceId, IReadOnlyList<HomeStockCountChange> changes, HomeStockCountOptions options, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Logs units sent from home stock into Amazon: one <see cref="Domain.Core.HomeStockMovementType.ShippedToAmazon"/>
    /// entry per SKU, all in one transaction. SKUs whose home stock changed since the amounts were entered are skipped.
    /// </summary>
    /// <param name="marketplaceId">Marketplace the stock is held for.</param>
    /// <param name="shipments">What was sent, with the home stock each amount was entered against.</param>
    /// <param name="reference">FBA shipment id applied to every entry, if any.</param>
    /// <param name="user">Email of the user logging it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What was logged (<see cref="HomeStockCountResult.UnitsOut"/>) and skipped, or why nothing was.</returns>
    Task<Result<HomeStockCountResult>> ShipToAmazonAsync(string marketplaceId, IReadOnlyList<HomeStockShipment> shipments, string? reference, string user, CancellationToken cancellationToken);

    /// <summary>Reverses an entry.</summary>
    /// <param name="marketplaceId">Marketplace of the entry.</param>
    /// <param name="id">The entry.</param>
    /// <param name="user">Email of the user reversing it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or why it cannot be reversed.</returns>
    Task<Result> ReverseAsync(string marketplaceId, long id, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Edits an entry from the ledger page. The ledger is never overwritten: the entry is reversed and a
    /// corrected copy logged in its place, so the history and the balance stay intact.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="input">The entry and its new type, units, and note.</param>
    /// <param name="user">Email of the user editing it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A message to show, or why it can't be edited.</returns>
    Task<Result<string>> CorrectAsync(string marketplaceId, HomeStockCorrectionInput input, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the reason (movement type and/or note) of several entries at once, each recorded as a correction
    /// like a single edit, with the same rules. Units stay each entry's own (received and shipped take their
    /// type's direction). Entries that can't change are skipped and reported, not failed as a batch.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="ids">The entries, at most <see cref="HomeStockLedgerService.MaxBulkEntries"/>.</param>
    /// <param name="type">The movement to set, or <see langword="null"/> to keep each entry's own.</param>
    /// <param name="note">The note to set, or blank to keep each entry's own.</param>
    /// <param name="user">Email of the user editing them.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many changed, were already so, or were skipped and why; or why nothing could be done.</returns>
    Task<Result<HomeStockBulkCorrectionResult>> CorrectReasonAsync(
        string marketplaceId, IReadOnlyCollection<long> ids, HomeStockMovementType? type, string? note, string user, CancellationToken cancellationToken);

    /// <summary>One page of entries, newest first.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="filter">Filters.</param>
    /// <param name="request">Paging and search.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page.</returns>
    Task<PagedResult<HomeStockLedgerEntry>> ListAsync(string marketplaceId, HomeStockLedgerFilter filter, PageRequest request, CancellationToken cancellationToken);

    /// <summary>One page of actions (the entries each save wrote), newest first.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="filter">Filters.</param>
    /// <param name="request">Paging (in actions) and search.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page; each action lists its newest entries, at most <see cref="HomeStockLedgerService.MaxGroupRows"/>.</returns>
    Task<PagedResult<HomeStockLedgerAction>> GetActionsAsync(string marketplaceId, HomeStockLedgerFilter filter, PageRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Undoes a whole action: every entry logged with the given one is reversed, all or nothing, so home stock
    /// goes back to what it was without leaving the action half undone.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="entryId">Any entry of the action.</param>
    /// <param name="user">Email of the user undoing it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A message saying what was undone, or why it couldn't be.</returns>
    Task<Result<string>> UndoActionAsync(string marketplaceId, long entryId, string user, CancellationToken cancellationToken);

    /// <summary>Reverses several entries at once, all or nothing, as one action (e.g. every send of one shipment).</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="ids">The entries, at most <see cref="HomeStockLedgerService.MaxBulkEntries"/>.</param>
    /// <param name="user">Email of the user reversing them.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A message saying what was reversed, or why nothing was.</returns>
    Task<Result<string>> ReverseManyAsync(string marketplaceId, IReadOnlyCollection<long> ids, string user, CancellationToken cancellationToken);
}
