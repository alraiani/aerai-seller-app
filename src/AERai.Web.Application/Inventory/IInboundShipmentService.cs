using AERai.Web.Application.Common;

namespace AERai.Web.Application.Inventory;

/// <summary>
/// Reviews the shipments the inbound shipments sync reads from Send to Amazon: an Operator accepts one (logging its
/// units from home stock as a Shipped to Amazon send carrying its FBA id) or dismisses it. Nothing touches home stock
/// until a shipment is accepted.
/// </summary>
public interface IInboundShipmentService
{
    /// <summary>Lists the marketplace's recent shipments for the review page.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="filter">View, search, and family.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The shipments and the view counts.</returns>
    Task<InboundShipmentList> ListAsync(string marketplaceId, InboundShipmentFilter filter, CancellationToken cancellationToken);

    /// <summary>Counts the shipments waiting for review (shown on the bell and the Inventory tab).</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The count.</returns>
    Task<int> CountToReviewAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>
    /// Accepts a shipment: logs the entered units of its SKUs from home stock as one Shipped to Amazon send under its
    /// FBA id, then marks it logged. SKUs entered as 0, SKUs not from home stock, and SKUs already logged under the
    /// id are left out; a SKU whose home stock changed since the page loaded is skipped and keeps the shipment to
    /// review.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="id">The shipment's id.</param>
    /// <param name="lines">Per SKU: the home stock the user saw and the units to log.</param>
    /// <param name="user">The Operator's email.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What was logged, or why nothing could be.</returns>
    Task<Result<HomeStockCountResult>> AcceptAsync(string marketplaceId, long id, IReadOnlyList<HomeStockShipment> lines, string user, CancellationToken cancellationToken);

    /// <summary>Sets a shipment to review aside without touching home stock.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="id">The shipment's id.</param>
    /// <param name="user">The Operator's email.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or why it couldn't be dismissed.</returns>
    Task<Result> DismissAsync(string marketplaceId, long id, string user, CancellationToken cancellationToken);

    /// <summary>Puts a set-aside shipment back to review.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="id">The shipment's id.</param>
    /// <param name="user">The Operator's email.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or why it couldn't be reopened.</returns>
    Task<Result> ReopenAsync(string marketplaceId, long id, string user, CancellationToken cancellationToken);
}
