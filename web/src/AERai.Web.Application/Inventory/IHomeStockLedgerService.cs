using AERai.Web.Application.Common;

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

    /// <summary>Reverses an entry.</summary>
    /// <param name="marketplaceId">Marketplace of the entry.</param>
    /// <param name="id">The entry.</param>
    /// <param name="user">Email of the user reversing it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or why it cannot be reversed.</returns>
    Task<Result> ReverseAsync(string marketplaceId, long id, string user, CancellationToken cancellationToken);

    /// <summary>One page of entries, newest first.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="filter">Filters.</param>
    /// <param name="request">Paging and search.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page.</returns>
    Task<PagedResult<HomeStockLedgerEntry>> ListAsync(string marketplaceId, HomeStockLedgerFilter filter, PageRequest request, CancellationToken cancellationToken);
}
