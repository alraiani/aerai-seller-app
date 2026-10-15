namespace AERai.Web.Application.Settlements;

/// <summary>Order of the order groups behind one per-SKU summary row.</summary>
public enum SettlementGroupSort
{
    /// <summary>Earliest posted first, the order Amazon settled them in.</summary>
    Posted = 0,

    /// <summary>Largest amount first.</summary>
    Amount = 1,

    /// <summary>By order id, A to Z; groups without an order (by transaction type) after the orders.</summary>
    Order = 2,
}
