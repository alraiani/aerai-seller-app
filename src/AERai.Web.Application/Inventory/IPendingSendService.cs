using AERai.Web.Application.Common;

namespace AERai.Web.Application.Inventory;

/// <summary>
/// Sends from home stock that Amazon's numbers don't show yet ("pending to Amazon"): matching them against
/// new FBA snapshots, listing the open ones, and the manual fixes for those Amazon never shows.
/// </summary>
public interface IPendingSendService
{
    /// <summary>A marketplace's open sends, oldest first.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="familyId">Only this family's SKUs, or all when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The open sends.</returns>
    Task<IReadOnlyList<PendingSendItem>> ListAsync(string marketplaceId, int? familyId, CancellationToken cancellationToken);

    /// <summary>Matches a marketplace's open sends against its latest FBA snapshots (see <see cref="PendingReconciliation"/>).</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many sends changed.</returns>
    Task<int> ReconcileAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>Closes an open send by hand, e.g. when Amazon received it but its numbers didn't move as expected.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="id">The send's ledger entry id.</param>
    /// <param name="user">Email of the user closing it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or why it couldn't be closed.</returns>
    Task<Result> MarkReconciledAsync(string marketplaceId, long id, string user, CancellationToken cancellationToken);

    /// <summary>Cancels a send that never went out: reverses its ledger entry, so the units return to home stock.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="id">The send's ledger entry id.</param>
    /// <param name="user">Email of the user cancelling it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or why it couldn't be cancelled.</returns>
    Task<Result> CancelAsync(string marketplaceId, long id, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the FBA shipment id to an open send logged without one, so it is matched by its shipment, and
    /// queues an inventory sync.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="id">The send's ledger entry id.</param>
    /// <param name="shipmentId">The FBA shipment id.</param>
    /// <param name="user">Email of the user adding it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or why it couldn't be added.</returns>
    Task<Result> SetShipmentIdAsync(string marketplaceId, long id, string? shipmentId, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Adds one FBA shipment id to several open sends logged without one (a shipment usually holds several
    /// SKUs), and queues one inventory sync. Sends that are closed or already have a reference are skipped.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="ids">The sends' ledger entry ids.</param>
    /// <param name="shipmentId">The FBA shipment id.</param>
    /// <param name="user">Email of the user adding it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many sends got the id and how many were skipped, or why nothing was saved.</returns>
    Task<Result<(int Added, int Skipped)>> SetShipmentIdsAsync(string marketplaceId, IReadOnlyCollection<long> ids, string? shipmentId, string user, CancellationToken cancellationToken);
}
