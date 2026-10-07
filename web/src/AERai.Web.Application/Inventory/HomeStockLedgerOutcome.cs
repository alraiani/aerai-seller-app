namespace AERai.Web.Application.Inventory;

/// <summary>What happened when a ledger entry was written.</summary>
public enum HomeStockLedgerOutcome
{
    /// <summary>The entry was recorded and the balance updated.</summary>
    Recorded = 0,

    /// <summary>Nothing to record (a recount that matched the current balance).</summary>
    Unchanged = 1,

    /// <summary>The SKU (or the entry being reversed) does not exist.</summary>
    NotFound = 2,

    /// <summary>The entry would take home stock below zero.</summary>
    WouldGoNegative = 3,

    /// <summary>The entry being reversed was already reversed, or is itself a reversal.</summary>
    CannotReverse = 4,
}
