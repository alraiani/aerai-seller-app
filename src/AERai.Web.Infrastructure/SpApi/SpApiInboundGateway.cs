using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>Live <see cref="IAmazonInboundGateway"/> over <see cref="InboundApiClient"/>.</summary>
/// <param name="client">Typed Fulfillment Inbound API client.</param>
internal sealed class SpApiInboundGateway(InboundApiClient client) : IAmazonInboundGateway
{
    /// <inheritdoc/>
    public Task<InboundPlanPage> ListPlansPageAsync(Marketplace marketplace, string? nextToken, CancellationToken cancellationToken) =>
        client.ListPlansAsync(marketplace, nextToken, cancellationToken);

    /// <inheritdoc/>
    public Task<InboundPlanShipments> GetPlanShipmentsAsync(Marketplace marketplace, string inboundPlanId, CancellationToken cancellationToken) =>
        client.GetPlanAsync(marketplace, inboundPlanId, cancellationToken);

    /// <inheritdoc/>
    public Task<InboundShipmentDetail> GetShipmentAsync(Marketplace marketplace, string inboundPlanId, string shipmentId, CancellationToken cancellationToken) =>
        client.GetShipmentAsync(marketplace, inboundPlanId, shipmentId, cancellationToken);

    /// <inheritdoc/>
    public Task<InboundShipmentItemsPage> ListShipmentItemsPageAsync(Marketplace marketplace, string inboundPlanId, string shipmentId, string? nextToken, CancellationToken cancellationToken) =>
        client.ListShipmentItemsAsync(marketplace, inboundPlanId, shipmentId, nextToken, cancellationToken);
}
