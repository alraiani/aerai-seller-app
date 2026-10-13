namespace AERai.Web.Application.Inventory;

/// <summary>Open sends matching a filter, with counts for the view switch.</summary>
/// <param name="Items">The matching sends, oldest first.</param>
/// <param name="Total">Open sends in scope (family and search applied, any view).</param>
/// <param name="NeedsShipmentId">Of those, sends without a reference.</param>
/// <param name="Overdue">Of those, sends flagged as overdue.</param>
public sealed record PendingSendList(IReadOnlyList<PendingSendItem> Items, int Total, int NeedsShipmentId, int Overdue);
