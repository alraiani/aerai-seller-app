namespace AERai.Web.Application.Settlements;

/// <summary>Order of the rows of one settlement's per-SKU summary.</summary>
public enum SettlementSkuSort
{
    /// <summary>Largest sales first.</summary>
    Sales = 0,

    /// <summary>By SKU, A to Z (the row without a SKU first).</summary>
    Sku = 1,

    /// <summary>Most units first.</summary>
    Units = 2,

    /// <summary>Most orders first.</summary>
    Orders = 3,

    /// <summary>Largest fees first (the most negative first).</summary>
    Fees = 4,

    /// <summary>Largest refunds first (the most negative first).</summary>
    Refunds = 5,

    /// <summary>Largest net first.</summary>
    Net = 6,
}
