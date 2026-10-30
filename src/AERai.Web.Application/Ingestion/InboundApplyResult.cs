namespace AERai.Web.Application.Ingestion;

/// <summary>What one run of the inbound shipments sync changed.</summary>
/// <param name="Shipments">Shipments read and recorded.</param>
/// <param name="New">Shipments seen for the first time.</param>
/// <param name="NewToReview">New shipments now waiting for review.</param>
/// <param name="AlreadyLogged">New shipments whose FBA id a send in the ledger already carries.</param>
/// <param name="SetAside">New shipments set aside on the marketplace's first read because they had already left.</param>
/// <param name="Cancelled">Shipments to review that Amazon has since cancelled.</param>
public sealed record InboundApplyResult(int Shipments, int New, int NewToReview, int AlreadyLogged, int SetAside, int Cancelled);
