using System.ComponentModel.DataAnnotations;

namespace AERai.Web.Application.Dashboard;

/// <summary>
/// Tunables for the dashboard. Bound from the <c>Dashboard</c> configuration section.
/// </summary>
public sealed class DashboardOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Dashboard";

    /// <summary>SKUs with this many days of supply or fewer are flagged as at risk.</summary>
    [Range(1, 365)]
    public decimal AtRiskDaysOfSupply { get; set; } = 21;

    /// <summary>Maximum at-risk SKUs shown on the dashboard.</summary>
    [Range(1, 100)]
    public int AtRiskTake { get; set; } = 10;

    /// <summary>Number of days in the revenue trend chart (including today).</summary>
    [Range(7, 90)]
    public int TrendDays { get; set; } = 14;
}
