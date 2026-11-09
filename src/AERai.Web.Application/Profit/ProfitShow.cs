namespace AERai.Web.Application.Profit;

/// <summary>Which SKUs the profit page lists.</summary>
public enum ProfitShow
{
    /// <summary>Every SKU with an amount in the range.</summary>
    All = 0,

    /// <summary>SKUs whose profit is below zero.</summary>
    LosingMoney = 1,

    /// <summary>SKUs with units sold and no cost set on their order date.</summary>
    CostMissing = 2,
}
