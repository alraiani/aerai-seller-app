namespace AERai.Web.Application.Inventory;

/// <summary>
/// Queues an immediate FBA inventory and restock sync for a marketplace, so units just sent to Amazon stop
/// being pending as soon as Amazon shows them instead of at the next scheduled run.
/// </summary>
public interface IInventoryRefreshRequester
{
    /// <summary>Queues a run of the marketplace's FBA inventory and restock schedules; does nothing when Amazon isn't connected.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="user">Who asked (recorded on the runs).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many runs were queued.</returns>
    Task<int> RequestAsync(string marketplaceId, string user, CancellationToken cancellationToken);
}
