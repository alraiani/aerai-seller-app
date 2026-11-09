namespace AERai.Web.UI.Pages.Profit;

/// <summary>The views of the profit page.</summary>
public enum ProfitView
{
    /// <summary>Families and SKUs.</summary>
    Products = 0,

    /// <summary>The same figures by month (or week).</summary>
    Months = 1,

    /// <summary>The ledger against each settlement, and the bridge from payouts to profit.</summary>
    Reconciliation = 2,
}
