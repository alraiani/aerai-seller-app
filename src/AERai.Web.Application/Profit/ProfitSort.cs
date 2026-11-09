namespace AERai.Web.Application.Profit;

/// <summary>How SKUs are ordered within each family on the profit page.</summary>
public enum ProfitSort
{
    /// <summary>By profit.</summary>
    Profit = 0,

    /// <summary>By SKU.</summary>
    Sku = 1,

    /// <summary>By units sold.</summary>
    Units = 2,

    /// <summary>By sales.</summary>
    Sales = 3,

    /// <summary>By refunds.</summary>
    Refunds = 4,

    /// <summary>By Amazon's fees.</summary>
    AmazonFees = 5,

    /// <summary>By other expenses.</summary>
    Other = 6,

    /// <summary>By advertising.</summary>
    Advertising = 7,

    /// <summary>By cost of goods.</summary>
    Cogs = 8,

    /// <summary>By margin.</summary>
    Margin = 9,
}
