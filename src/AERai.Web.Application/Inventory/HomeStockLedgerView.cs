namespace AERai.Web.Application.Inventory;

/// <summary>How the home-stock ledger page lists entries.</summary>
public enum HomeStockLedgerView
{
    /// <summary>One collapsible group per kind of movement, with totals (the default).</summary>
    ByMovement = 0,

    /// <summary>Every entry in logging order, paged.</summary>
    Entries = 1,
}
