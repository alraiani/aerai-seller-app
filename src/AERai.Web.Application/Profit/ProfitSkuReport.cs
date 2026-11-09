namespace AERai.Web.Application.Profit;

/// <summary>One SKU's profit over a range, by period.</summary>
/// <param name="From">First day.</param>
/// <param name="To">Last day (inclusive).</param>
/// <param name="Currency">The marketplace's currency.</param>
/// <param name="Sku">The SKU's row (catalog details and totals).</param>
/// <param name="Periods">Its figures by month or week, oldest first.</param>
/// <param name="Granularity">Months or weeks.</param>
/// <param name="Coverage">Where the figures come from.</param>
public sealed record ProfitSkuReport(
    DateOnly From, DateOnly To, string Currency, ProfitSkuRow Sku, IReadOnlyList<ProfitPeriod> Periods, ProfitGranularity Granularity, ProfitCoverage Coverage);
