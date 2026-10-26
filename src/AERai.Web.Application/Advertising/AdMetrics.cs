namespace AERai.Web.Application.Advertising;

/// <summary>
/// Advertising figures for a SKU, a family, or everything listed, with the ratios worked out from them.
/// One shape for all three, so a family's row and the total are always the sum of the rows under them.
/// </summary>
/// <param name="Impressions">Times the ads were shown.</param>
/// <param name="Clicks">Times the ads were clicked.</param>
/// <param name="Spend">What the clicks cost.</param>
/// <param name="AdSales">Sales Amazon attributes to the ads (7 days after a click).</param>
/// <param name="AdOrders">Orders Amazon attributes to the ads.</param>
/// <param name="AdUnits">Units Amazon attributes to the ads.</param>
/// <param name="TotalSales">Every sale of the SKUs over the same days (from Orders, cancelled left out), with an ad or not.</param>
/// <param name="TotalUnits">Units of those sales.</param>
public sealed record AdMetrics(
    long Impressions, long Clicks, decimal Spend, decimal AdSales, int AdOrders, int AdUnits, decimal TotalSales, int TotalUnits)
{
    /// <summary>Nothing.</summary>
    public static readonly AdMetrics Zero = new(0, 0, 0m, 0m, 0, 0, 0m, 0);

    /// <summary>Cost per click, or <see langword="null"/> without clicks.</summary>
    public decimal? CostPerClick => Clicks > 0 ? Spend / Clicks : null;

    /// <summary>Click-through rate (clicks ÷ impressions), or <see langword="null"/> without impressions.</summary>
    public decimal? ClickThroughRate => Impressions > 0 ? (decimal)Clicks / Impressions : null;

    /// <summary>Advertising cost of sales (spend ÷ ad sales), or <see langword="null"/> without ad sales.</summary>
    public decimal? Acos => AdSales > 0 ? Spend / AdSales : null;

    /// <summary>Total advertising cost of sales (spend ÷ all sales), or <see langword="null"/> without sales.</summary>
    public decimal? Tacos => TotalSales > 0 ? Spend / TotalSales : null;

    /// <summary>Adds figures up; the ratios are worked out again from the sums, never averaged.</summary>
    /// <param name="metrics">Figures to add.</param>
    /// <returns>The sum.</returns>
    public static AdMetrics Sum(IEnumerable<AdMetrics> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        return metrics.Aggregate(Zero, (total, m) => new AdMetrics(
            total.Impressions + m.Impressions,
            total.Clicks + m.Clicks,
            total.Spend + m.Spend,
            total.AdSales + m.AdSales,
            total.AdOrders + m.AdOrders,
            total.AdUnits + m.AdUnits,
            total.TotalSales + m.TotalSales,
            total.TotalUnits + m.TotalUnits));
    }
}
