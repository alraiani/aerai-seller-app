using System.ComponentModel.DataAnnotations;

namespace AERai.Web.Application.Dashboard;

/// <summary>
/// Tunables for the dashboard. Bound from the <c>Dashboard</c> configuration section. Currency and
/// time zone are not settings: they come from the selected marketplace.
/// </summary>
public sealed class DashboardOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Dashboard";

    /// <summary>In-stock SKUs with this many days of supply or fewer are flagged as low.</summary>
    [Range(1, 365)]
    public decimal AtRiskDaysOfSupply { get; set; } = 21;

    /// <summary>Sales data older than this is flagged as stale.</summary>
    [Range(1, 168)]
    public int StaleAfterHours { get; set; } = 6;

    /// <summary>Best sellers shown.</summary>
    [Range(1, 20)]
    public int TopProductCount { get; set; } = 5;
}
