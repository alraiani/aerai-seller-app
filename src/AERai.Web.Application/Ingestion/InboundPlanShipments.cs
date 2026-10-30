namespace AERai.Web.Application.Ingestion;

/// <summary>The shipments of one inbound plan, as <c>getInboundPlan</c> returns them.</summary>
/// <param name="InboundPlanId">Amazon's plan id.</param>
/// <param name="ShipmentIds">Amazon's internal ids of the plan's shipments (empty until a placement is confirmed).</param>
/// <param name="RawJson">The response body exactly as Amazon sent it, kept in the raw landing zone for audit.</param>
public sealed record InboundPlanShipments(string InboundPlanId, IReadOnlyList<string> ShipmentIds, string RawJson);
