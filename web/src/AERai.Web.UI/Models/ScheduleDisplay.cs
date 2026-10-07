using System.Globalization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.UI.Models;

/// <summary>
/// Formatting helpers for schedules on the Amazon sync pages (presentation only).
/// </summary>
public static class ScheduleDisplay
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Time zones offered in the schedule editor (IANA ids, which work on Linux and Windows).</summary>
    public static IReadOnlyList<(string Id, string Label)> TimeZones { get; } =
    [
        ("America/New_York", "Eastern (New York)"),
        ("America/Chicago", "Central (Chicago)"),
        ("America/Denver", "Mountain (Denver)"),
        ("America/Phoenix", "Arizona (Phoenix)"),
        ("America/Los_Angeles", "Pacific (Los Angeles)"),
        ("America/Anchorage", "Alaska (Anchorage)"),
        ("Pacific/Honolulu", "Hawaii (Honolulu)"),
        ("UTC", "UTC"),
    ];

    /// <summary>Report types offered in the editor, with a one-line description of each.</summary>
    public static IReadOnlyList<(AmazonReportType Type, string Description)> ReportTypes { get; } =
    [
        (AmazonReportType.Orders, "Orders created or updated since the last run. Hourly is typical."),
        (AmazonReportType.FbaInventory, "A snapshot of FBA stock by state. Once or twice a day is plenty."),
        (AmazonReportType.FbaReservedInventory, "Why stock is reserved (customer orders, FC transfers, FC processing). Run just after FBA inventory."),
        (AmazonReportType.RestockRecommendations, "How many units Amazon recommends sending in, and by when. Once a day is plenty."),
        (AmazonReportType.Settlements, "Amazon publishes these every ~14 days; a daily check picks up new ones once."),
    ];

    /// <summary>Plain-English description of how often a schedule runs.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <returns>e.g. "Every 1 h" or "Daily at 6:00 AM ET".</returns>
    public static string Frequency(SyncSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        return schedule.Frequency switch
        {
            ScheduleFrequency.Interval when schedule.IntervalMinutes is { } minutes => Interval(minutes),
            ScheduleFrequency.Daily when schedule.DailyTime is { } time =>
                $"Daily at {time.ToString("h:mm tt", En)} {ShortZone(schedule.TimeZoneId)}",
            _ => "—",
        };
    }

    /// <summary>Formats an interval in minutes.</summary>
    /// <param name="minutes">Minutes between runs.</param>
    /// <returns>e.g. "Every 15 min", "Every 1 h", "Every 1 h 30 min".</returns>
    public static string Interval(int minutes) => minutes switch
    {
        < 60 => $"Every {minutes} min",
        _ when minutes % 60 == 0 => $"Every {minutes / 60} h",
        _ => $"Every {minutes / 60} h {minutes % 60} min",
    };

    /// <summary>Short label for a time zone.</summary>
    /// <param name="timeZoneId">IANA zone id.</param>
    /// <returns>e.g. "ET", "PT", "UTC".</returns>
    public static string ShortZone(string timeZoneId) => timeZoneId switch
    {
        "America/New_York" => "ET",
        "America/Chicago" => "CT",
        "America/Denver" => "MT",
        "America/Phoenix" => "AZ",
        "America/Los_Angeles" => "PT",
        "America/Anchorage" => "AKT",
        "Pacific/Honolulu" => "HT",
        _ => timeZoneId,
    };

    /// <summary>Formats a UTC instant in the schedule's time zone, for tooltips and detail text.</summary>
    /// <param name="instant">The time, or <see langword="null"/>.</param>
    /// <param name="timeZoneId">IANA zone id.</param>
    /// <returns>e.g. "Fri Oct 2, 3:00 PM ET", or an em dash.</returns>
    public static string Local(DateTimeOffset? instant, string timeZoneId)
    {
        if (instant is null)
        {
            return "—";
        }

        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var found) ? found : TimeZoneInfo.Utc;
        return TimeZoneInfo.ConvertTime(instant.Value, zone).ToString("ddd MMM d, h:mm tt", En) + " " + ShortZone(timeZoneId);
    }

    /// <summary>Relative time in either direction.</summary>
    /// <param name="when">Instant.</param>
    /// <param name="now">Reference time.</param>
    /// <returns>e.g. "in 42 min", "3 h ago", "just now".</returns>
    public static string Relative(DateTimeOffset? when, DateTimeOffset now)
    {
        if (when is not { } value)
        {
            return "—";
        }

        var delta = value - now;
        var future = delta > TimeSpan.Zero;
        var span = future ? delta : -delta;
        if (span.TotalMinutes < 1)
        {
            return future ? "any moment" : "just now";
        }

        var text = span.TotalMinutes switch
        {
            < 60 => $"{(int)span.TotalMinutes} min",
            < 48 * 60 => $"{(int)span.TotalHours} h",
            _ => $"{(int)span.TotalDays} days",
        };
        return future ? $"in {text}" : $"{text} ago";
    }

    /// <summary>Human label for a report type.</summary>
    /// <param name="type">Report type.</param>
    /// <returns>e.g. "FBA inventory".</returns>
    public static string ReportLabel(AmazonReportType type) => DashboardFormat.ReportLabel(type);

    /// <summary>Colored tag class for a report type.</summary>
    /// <param name="type">Report type.</param>
    /// <returns>CSS classes.</returns>
    public static string ReportTag(AmazonReportType type) => type switch
    {
        AmazonReportType.Orders => "tag tag-orders",
        AmazonReportType.FbaInventory or AmazonReportType.FbaReservedInventory or AmazonReportType.RestockRecommendations => "tag tag-inventory",
        AmazonReportType.Settlements => "tag tag-settlements",
        _ => "tag",
    };

    /// <summary>Pill class for a run status (token-colored, never grey).</summary>
    /// <param name="status">Run status.</param>
    /// <returns>CSS classes.</returns>
    public static string StatusPill(SyncRunStatus status) => status switch
    {
        SyncRunStatus.Succeeded => "pill pill-ok",
        SyncRunStatus.NoData => "pill pill-info",
        SyncRunStatus.Failed => "pill pill-bad",
        _ => "pill pill-running",
    };

    /// <summary>Label for a run status.</summary>
    /// <param name="status">Run status.</param>
    /// <returns>e.g. "No new data".</returns>
    public static string StatusLabel(SyncRunStatus status) => status switch
    {
        SyncRunStatus.NoData => "No new data",
        _ => status.ToString(),
    };

    /// <summary>Bootstrap badge class for a run status (Run history table).</summary>
    /// <param name="status">Run status.</param>
    /// <returns>CSS classes.</returns>
    public static string StatusBadge(SyncRunStatus status) => StatusPill(status);

    /// <summary>The SP-API status light: color, label, and detail line.</summary>
    /// <param name="connection">Connection state.</param>
    /// <param name="paused">Whether scheduled syncs are paused.</param>
    /// <returns>A CSS modifier (<c>live</c>, <c>down</c>, <c>sim</c>, <c>paused</c>), a label, and a detail line.</returns>
    public static (string Css, string Label, string Detail) Connection(IAmazonConnectionInfo connection, bool paused)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (!connection.CanRun)
        {
            return ("down", "SP-API not connected", $"{connection.Problem} Schedules can be edited, but nothing will run.");
        }

        if (paused)
        {
            return ("paused", "SP-API connected · syncs paused", "Scheduled runs are on hold. Run now and backfills still work.");
        }

        return connection.Mode == "Live"
            ? ("live", "SP-API live", "Connected to Amazon. Scheduled pulls run automatically.")
            : ("sim", "SP-API simulated", "Sample reports are generated locally; no data comes from Amazon.");
    }
}
