namespace AERai.Web.Domain.Alerts;

/// <summary>How serious a stock alert is.</summary>
public enum StockAlertLevel
{
    /// <summary>A restock action (send or order) is due soon or overdue.</summary>
    Low = 1,

    /// <summary>Nothing sellable at Amazon for a SKU that has been selling.</summary>
    Out = 2,
}
