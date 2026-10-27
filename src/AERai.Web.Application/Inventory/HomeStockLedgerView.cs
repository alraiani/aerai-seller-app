namespace AERai.Web.Application.Inventory;

/// <summary>How the home-stock ledger page lists entries.</summary>
public enum HomeStockLedgerView
{
    /// <summary>One collapsible group per action (one save), newest first, with totals (the default).</summary>
    ByAction = 0,

    /// <summary>Every entry in logging order, paged.</summary>
    Entries = 1,
}
