using System.ComponentModel.DataAnnotations;

namespace AERai.Web.Application.Dashboard;

/// <summary>
/// Tunables for the dashboard. Bound from the <c>Dashboard</c> configuration section.
/// </summary>
public sealed class DashboardOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Dashboard";

    /// <summary>
    /// IANA time zone that defines the business day and hour (default US Eastern). Orders placed
    /// late in the evening count on the local day they were placed, not the UTC day.
    /// </summary>
    [Required]
    public string TimeZoneId { get; set; } = "America/New_York";

    /// <summary>Currency the dashboard reports in. Amounts in other currencies are never mixed in.</summary>
    [Required]
    [RegularExpression("^[A-Z]{3}$")]
    public string Currency { get; set; } = "USD";

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
