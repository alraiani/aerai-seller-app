namespace AERai.Web.Application.Inventory;

/// <summary>A send's new reconciled total after matching it against Amazon's numbers.</summary>
/// <param name="Id">The send's ledger entry id.</param>
/// <param name="ReconciledUnits">Units Amazon's numbers now show.</param>
/// <param name="Closed">Whether the send is fully matched and no longer pending.</param>
public sealed record PendingSendMatch(long Id, int ReconciledUnits, bool Closed);
