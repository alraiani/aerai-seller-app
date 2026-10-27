namespace AERai.Web.Domain.Ingestion;

/// <summary>The kind of business data an Amazon sync brings in, used to group schedules on the sync page.</summary>
public enum SyncCategory
{
    /// <summary>Orders (the orders report or the Orders API).</summary>
    Orders = 1,

    /// <summary>Stock at Amazon and Amazon's restock advice (FBA, reserved, AWD, restock recommendations).</summary>
    Inventory = 2,

    /// <summary>Money Amazon paid or holds back (settlements and deferred transactions).</summary>
    SettlementsAndPayments = 3,

    /// <summary>Advertising spend.</summary>
    Advertising = 4,
}
