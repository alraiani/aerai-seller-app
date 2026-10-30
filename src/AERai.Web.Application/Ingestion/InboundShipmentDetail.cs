namespace AERai.Web.Application.Ingestion;

/// <summary>One shipment of an inbound plan, as <c>getShipment</c> returns it.</summary>
/// <param name="ShipmentId">Amazon's internal shipment id (e.g. <c>sh…</c>).</param>
/// <param name="ShipmentConfirmationId">
/// The FBA shipment id Seller Central shows (e.g. <c>FBA15ABCD1234</c>); <see langword="null"/> until the plan's
/// placement is confirmed.
/// </param>
/// <param name="Name">The shipment's name, when Amazon gives one.</param>
/// <param name="Status">Amazon's shipment status, e.g. <c>WORKING</c>, <c>SHIPPED</c>, or <c>CANCELLED</c>.</param>
/// <param name="RawJson">The response body exactly as Amazon sent it, kept in the raw landing zone for audit.</param>
public sealed record InboundShipmentDetail(string ShipmentId, string? ShipmentConfirmationId, string? Name, string Status, string RawJson);
