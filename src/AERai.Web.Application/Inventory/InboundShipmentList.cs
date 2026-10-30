namespace AERai.Web.Application.Inventory;

/// <summary>The Amazon shipments the filter selects, with the counts for the view switch.</summary>
/// <param name="Shipments">The shipments in the chosen view, newest plan first.</param>
/// <param name="ToReview">Shipments to review (after the search and family).</param>
/// <param name="Logged">Shipments logged as sends.</param>
/// <param name="SetAside">Shipments set aside.</param>
/// <param name="All">Every shipment.</param>
public sealed record InboundShipmentList(IReadOnlyList<InboundShipmentRow> Shipments, int ToReview, int Logged, int SetAside, int All);
