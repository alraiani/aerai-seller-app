using System.Globalization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IAmazonInboundGateway"/>: plans in scripted pages (chained by token), each plan's shipments,
/// and each shipment's detail and SKUs.
/// </summary>
internal sealed class FakeAmazonInboundGateway : IAmazonInboundGateway
{
    public List<IReadOnlyList<InboundPlanSummary>> PlanPages { get; } = [];

    /// <summary>Plan id → its shipment ids.</summary>
    public Dictionary<string, IReadOnlyList<string>> PlanShipments { get; } = [];

    /// <summary>Shipment id → (FBA id, status).</summary>
    public Dictionary<string, (string? ConfirmationId, string Status)> Shipments { get; } = [];

    /// <summary>Shipment id → its SKUs, one page per entry.</summary>
    public Dictionary<string, List<IReadOnlyList<AmazonInboundItem>>> Items { get; } = [];

    public List<string?> PlanTokensSeen { get; } = [];

    public List<string> PlansRead { get; } = [];

    public Exception? Failure { get; set; }

    public Task<InboundPlanPage> ListPlansPageAsync(Marketplace marketplace, string? nextToken, CancellationToken cancellationToken)
    {
        PlanTokensSeen.Add(nextToken);
        var index = nextToken is null ? 0 : int.Parse(nextToken, CultureInfo.InvariantCulture);
        var plans = index < PlanPages.Count ? PlanPages[index] : [];
        var next = index + 1 < PlanPages.Count ? (index + 1).ToString(CultureInfo.InvariantCulture) : null;
        return Task.FromResult(new InboundPlanPage(plans, next, $"{{\"plans\":{index}}}"));
    }

    public Task<InboundPlanShipments> GetPlanShipmentsAsync(Marketplace marketplace, string inboundPlanId, CancellationToken cancellationToken)
    {
        PlansRead.Add(inboundPlanId);
        return Task.FromResult(new InboundPlanShipments(inboundPlanId, PlanShipments.GetValueOrDefault(inboundPlanId) ?? [], "{}"));
    }

    public Task<InboundShipmentDetail> GetShipmentAsync(Marketplace marketplace, string inboundPlanId, string shipmentId, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        var (confirmation, status) = Shipments[shipmentId];
        return Task.FromResult(new InboundShipmentDetail(shipmentId, confirmation, $"Shipment {shipmentId}", status, "{}"));
    }

    public Task<InboundShipmentItemsPage> ListShipmentItemsPageAsync(Marketplace marketplace, string inboundPlanId, string shipmentId, string? nextToken, CancellationToken cancellationToken)
    {
        var pages = Items.GetValueOrDefault(shipmentId) ?? [];
        var index = nextToken is null ? 0 : int.Parse(nextToken, CultureInfo.InvariantCulture);
        var items = index < pages.Count ? pages[index] : [];
        var next = index + 1 < pages.Count ? (index + 1).ToString(CultureInfo.InvariantCulture) : null;
        return Task.FromResult(new InboundShipmentItemsPage(items, next, "{}"));
    }
}
