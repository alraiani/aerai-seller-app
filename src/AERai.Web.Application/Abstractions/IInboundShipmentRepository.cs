using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Reads and writes <c>core.InboundShipment</c>: the inbound shipments sync records what Amazon lists, and the review
/// page reads shipments and moves them through <see cref="InboundShipmentReview"/>.
/// </summary>
public interface IInboundShipmentRepository
{
    /// <summary>
    /// Records one complete read of the marketplace's recent shipments, in one transaction. Each shipment is added or
    /// refreshed by its FBA shipment id and its SKUs replaced. A new shipment starts as
    /// <see cref="InboundShipmentReview.AlreadyLogged"/> when a Shipped to Amazon send already carries its FBA id,
    /// as <see cref="InboundShipmentReview.Dismissed"/> (by the system) when it is the marketplace's first read and
    /// the shipment has already left, and otherwise <see cref="InboundShipmentReview.ToReview"/>. A shipment still to
    /// review that Amazon cancelled becomes <see cref="InboundShipmentReview.CancelledByAmazon"/>.
    /// </summary>
    /// <param name="marketplaceId">Marketplace whose schedule read the shipments.</param>
    /// <param name="shipments">The shipments read, one per FBA shipment id.</param>
    /// <param name="seenAt">When they were read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What changed.</returns>
    Task<InboundApplyResult> ApplyAsync(string marketplaceId, IReadOnlyCollection<AmazonInboundShipment> shipments, DateTimeOffset seenAt, CancellationToken cancellationToken);

    /// <summary>Lists the marketplace's shipments, newest plan first, with their SKUs.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="limit">Most shipments returned.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The shipments.</returns>
    Task<IReadOnlyList<InboundShipment>> ListAsync(string marketplaceId, int limit, CancellationToken cancellationToken);

    /// <summary>Gets one shipment with its SKUs.</summary>
    /// <param name="marketplaceId">Marketplace (a shipment of another one is never returned).</param>
    /// <param name="id">The shipment's id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The shipment, or <see langword="null"/> when it doesn't exist.</returns>
    Task<InboundShipment?> GetAsync(string marketplaceId, long id, CancellationToken cancellationToken);

    /// <summary>Moves a shipment to another review state, only if it is still in the expected one.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="id">The shipment's id.</param>
    /// <param name="expected">The state it must be in.</param>
    /// <param name="review">The new state.</param>
    /// <param name="at">When.</param>
    /// <param name="by">Who (a user's email).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> when it moved; <see langword="false"/> when it was not in <paramref name="expected"/>.</returns>
    Task<bool> SetReviewAsync(string marketplaceId, long id, InboundShipmentReview expected, InboundShipmentReview review, DateTimeOffset at, string by, CancellationToken cancellationToken);

    /// <summary>
    /// Counts the shipments still to review that hold at least one SKU of a family that ships from home stock: the
    /// number the bell and the Inventory tab show.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The count.</returns>
    Task<int> CountToReviewAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>Reads the catalog and home-stock facts the review needs for each SKU.</summary>
    /// <param name="marketplaceId">Marketplace (home stock is per marketplace).</param>
    /// <param name="skus">The SKUs.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One entry per SKU found in the catalog, keyed case-insensitively.</returns>
    Task<IReadOnlyDictionary<string, InboundSkuInfo>> GetSkuInfoAsync(string marketplaceId, IReadOnlyCollection<string> skus, CancellationToken cancellationToken);

    /// <summary>
    /// Adds up the Shipped to Amazon units logged under each reference, per SKU, net of reversals and corrections.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="references">FBA shipment ids.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Reference → SKU → units sent (positive); references and SKUs with nothing logged are left out.</returns>
    Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>> GetLoggedUnitsAsync(string marketplaceId, IReadOnlyCollection<string> references, CancellationToken cancellationToken);
}
