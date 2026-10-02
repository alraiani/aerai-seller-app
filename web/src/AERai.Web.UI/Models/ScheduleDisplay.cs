using System.Globalization;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.UI.Models;

/// <summary>
/// Formatting helpers for schedules on the Schedules pages (presentation only).
/// </summary>
public static class ScheduleDisplay
{
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

    /// <summary>Plain-English description of how often a schedule runs.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <returns>e.g. "Every 1 h" or "Daily at 6:00 AM (America/New_York)".</returns>
    public static string Frequency(SyncSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        return schedule.Frequency switch
        {
            ScheduleFrequency.Interval when schedule.IntervalMinutes is { } minutes =>
                minutes % 60 == 0 ? $"Every {minutes / 60} h" : $"Every {minutes} min",
            ScheduleFrequency.Daily when schedule.DailyTime is { } time =>
                $"Daily at {time.ToString("h:mm tt", CultureInfo.InvariantCulture)} ({schedule.TimeZoneId})",
            _ => "—",
        };
    }

    /// <summary>Formats a UTC instant in the schedule's time zone, for "next run" / "last run" columns.</summary>
    /// <param name="instant">The time, or <see langword="null"/>.</param>
    /// <param name="timeZoneId">IANA zone id.</param>
    /// <returns>e.g. "Fri Oct 2, 3:00 PM", or an em dash.</returns>
    public static string Local(DateTimeOffset? instant, string timeZoneId)
    {
        if (instant is null)
        {
            return "—";
        }

        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var found) ? found : TimeZoneInfo.Utc;
        return TimeZoneInfo.ConvertTime(instant.Value, zone).ToString("ddd MMM d, h:mm tt", CultureInfo.InvariantCulture);
    }

    /// <summary>Bootstrap badge class for a run status.</summary>
    /// <param name="status">Run status.</param>
    /// <returns>CSS classes.</returns>
    public static string StatusBadge(SyncRunStatus status) => status switch
    {
        SyncRunStatus.Succeeded => "text-bg-success",
        SyncRunStatus.NoData => "text-bg-info",
        SyncRunStatus.Failed => "text-bg-danger",
        _ => "text-bg-secondary",
    };
}
