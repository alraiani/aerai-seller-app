namespace AERai.Web.Application.Ingestion;

/// <summary>
/// One shipment into Amazon as a run of the inbound shipments sync read it: the shipment, its plan, and every one of
/// its SKUs (repeats added up). What <see cref="Abstractions.IInboundShipmentRepository.ApplyAsync"/> writes.
/// </summary>
/// <param name="ShipmentConfirmationId">The FBA shipment id (e.g. <c>FBA15ABCD1234</c>).</param>
/// <param name="AmazonShipmentId">Amazon's internal shipment id.</param>
/// <param name="InboundPlanId">The plan the shipment belongs to.</param>
/// <param name="PlanName">The plan's name in Send to Amazon.</param>
/// <param name="PlanCreatedAt">When the plan was created.</param>
/// <param name="Name">The shipment's name, when Amazon gives one.</param>
/// <param name="Status">Amazon's shipment status.</param>
/// <param name="Items">The shipment's SKUs, one entry per SKU.</param>
public sealed record AmazonInboundShipment(
    string ShipmentConfirmationId,
    string AmazonShipmentId,
    string InboundPlanId,
    string? PlanName,
    DateTimeOffset? PlanCreatedAt,
    string? Name,
    string Status,
    IReadOnlyList<AmazonInboundItem> Items)
{
    /// <summary>
    /// Whether Amazon has dropped the shipment (<c>CANCELLED</c>, <c>DELETED</c>, or <c>ABANDONED</c>), so it will
    /// never ship.
    /// </summary>
    public bool IsCancelled => Status is "CANCELLED" or "DELETED" or "ABANDONED";

    /// <summary>
    /// Whether the shipment has already left (Amazon shows it in transit or further). Used on a marketplace's first
    /// sync, when shipments that already left were almost certainly logged by hand before.
    /// </summary>
    public bool HasLeft => Status is "SHIPPED" or "IN_TRANSIT" or "DELIVERED" or "CHECKED_IN" or "RECEIVING" or "CLOSED" or "MIXED";
}
