namespace AERai.Web.Application.Inventory;

/// <summary>Narrows the Amazon shipments list.</summary>
/// <param name="View">Which shipments to show.</param>
/// <param name="Search">Words matched against the FBA id, plan and shipment names, and each SKU and title (every word must match).</param>
/// <param name="FamilyId">Only shipments holding a SKU of this family, or all when <see langword="null"/>.</param>
public sealed record InboundShipmentFilter(InboundShipmentView View = InboundShipmentView.ToReview, string? Search = null, int? FamilyId = null);
