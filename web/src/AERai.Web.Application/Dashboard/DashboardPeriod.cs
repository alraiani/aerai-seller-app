namespace AERai.Web.Application.Dashboard;

/// <summary>
/// The time window the dashboard summarizes. Values are the number of local calendar days covered
/// (including today), and each period is compared with the same-length window just before it.
/// </summary>
public enum DashboardPeriod
{
    /// <summary>Today so far, compared with yesterday up to the same time of day.</summary>
    Today = 1,

    /// <summary>The last 7 days including today, compared with the 7 days before.</summary>
    Last7Days = 7,

    /// <summary>The last 30 days including today, compared with the 30 days before.</summary>
    Last30Days = 30,
}
