using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Inbound shipments gateway registered when <see cref="SpApiMode.Disabled"/>: every call fails with a clear message,
/// which the run records in its history.
/// </summary>
internal sealed class UnavailableInboundGateway : IAmazonInboundGateway
{
    private const string Message = "Amazon SP-API is disabled. Set SpApi:Mode to Live (with credentials) or Simulated.";

    /// <inheritdoc/>
    public Task<InboundPlanPage> ListPlansPageAsync(Marketplace marketplace, string? nextToken, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);

    /// <inheritdoc/>
    public Task<InboundPlanShipments> GetPlanShipmentsAsync(Marketplace marketplace, string inboundPlanId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);

    /// <inheritdoc/>
    public Task<InboundShipmentDetail> GetShipmentAsync(Marketplace marketplace, string inboundPlanId, string shipmentId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);

    /// <inheritdoc/>
    public Task<InboundShipmentItemsPage> ListShipmentItemsPageAsync(Marketplace marketplace, string inboundPlanId, string shipmentId, string? nextToken, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);
}
