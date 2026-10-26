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

    /// <summary>Largest expenses first (the most negative first).</summary>
    Expenses = 4,

    /// <summary>Largest refunds first (the most negative first).</summary>
    Refunds = 5,

    /// <summary>Largest net first.</summary>
    Net = 6,

    /// <summary>Largest refunded sales first (the most negative first).</summary>
    RefundedSales = 7,

    /// <summary>Largest refunded expenses first (usually fees given back, so the most positive first).</summary>
    RefundedExpenses = 8,

    /// <summary>Largest shipping costs first (the most negative first).</summary>
    Shipping = 9,

    /// <summary>Largest FBA fees first (the most negative first).</summary>
    FbaFees = 10,

    /// <summary>Largest commission first (the most negative first).</summary>
    Commission = 11,

    /// <summary>Largest other expenses first (the most negative first).</summary>
    OtherExpenses = 12,
}
