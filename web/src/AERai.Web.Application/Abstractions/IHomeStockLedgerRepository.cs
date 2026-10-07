using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;

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

    /// <summary>One page of a marketplace's entries, newest first.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="filter">SKU, family, type, and date range.</param>
    /// <param name="request">Paging; search matches SKU, title, reference, or note.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page.</returns>
    Task<PagedResult<HomeStockLedgerEntry>> ListAsync(string marketplaceId, HomeStockLedgerFilter filter, PageRequest request, CancellationToken cancellationToken);
}
