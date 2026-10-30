using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// The seller's shipments into Amazon (Send to Amazon). The live implementation calls the SP-API Fulfillment Inbound
/// API (v2024-03-20) through the SP-API pipeline; the simulated one makes up shipments for local development.
/// </summary>
public interface IAmazonInboundGateway
{
    /// <summary>Gets one page of the seller's inbound plans, most recently updated first.</summary>
    /// <param name="marketplace">Marketplace being synced; its region selects the endpoint and credentials.</param>
    /// <param name="nextToken">Token from the previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, with the token for the next one.</returns>
    Task<InboundPlanPage> ListPlansPageAsync(Marketplace marketplace, string? nextToken, CancellationToken cancellationToken);

    /// <summary>Gets the shipments of one inbound plan.</summary>
    /// <param name="marketplace">Marketplace being synced.</param>
    /// <param name="inboundPlanId">The plan.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The plan's shipment ids.</returns>
    Task<InboundPlanShipments> GetPlanShipmentsAsync(Marketplace marketplace, string inboundPlanId, CancellationToken cancellationToken);

    /// <summary>Gets one shipment of an inbound plan.</summary>
    /// <param name="marketplace">Marketplace being synced.</param>
    /// <param name="inboundPlanId">The plan.</param>
    /// <param name="shipmentId">Amazon's internal shipment id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The shipment, with its FBA shipment id once it has one.</returns>
    Task<InboundShipmentDetail> GetShipmentAsync(Marketplace marketplace, string inboundPlanId, string shipmentId, CancellationToken cancellationToken);

    /// <summary>Gets one page of a shipment's SKUs.</summary>
    /// <param name="marketplace">Marketplace being synced.</param>
    /// <param name="inboundPlanId">The plan.</param>
    /// <param name="shipmentId">Amazon's internal shipment id.</param>
    /// <param name="nextToken">Token from the previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, with the token for the next one.</returns>
    Task<InboundShipmentItemsPage> ListShipmentItemsPageAsync(Marketplace marketplace, string inboundPlanId, string shipmentId, string? nextToken, CancellationToken cancellationToken);
}
