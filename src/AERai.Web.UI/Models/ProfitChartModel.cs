using AERai.Web.Application.Profit;

namespace AERai.Web.UI.Models;

/// <summary>What the profit chart draws: sales and profit per period.</summary>
/// <param name="Periods">The periods, oldest first.</param>
/// <param name="Granularity">Months or weeks.</param>
/// <param name="Currency">The marketplace's currency.</param>
/// <param name="Subject">What the figures are of, for the chart's accessible name (e.g. "all SKUs").</param>
public sealed record ProfitChartModel(IReadOnlyList<ProfitPeriod> Periods, ProfitGranularity Granularity, string Currency, string Subject);
