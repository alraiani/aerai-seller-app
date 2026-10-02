using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Ingestion;

/// <summary>User-editable schedule settings, as submitted from the Schedules page.</summary>
/// <param name="Name">Display name.</param>
/// <param name="ReportType">Report to pull.</param>
/// <param name="IsEnabled">Whether it runs automatically.</param>
/// <param name="Frequency">Interval or daily.</param>
/// <param name="IntervalMinutes">Minutes between runs (interval schedules).</param>
/// <param name="DailyTime">Local time of day (daily schedules).</param>
/// <param name="TimeZoneId">IANA time zone for <paramref name="DailyTime"/>.</param>
/// <param name="LookbackDays">How far back the first run reaches.</param>
/// <param name="AutoPromote">Promote staged batches immediately.</param>
public sealed record SyncScheduleInput(
    string Name,
    AmazonReportType ReportType,
    bool IsEnabled,
    ScheduleFrequency Frequency,
    int? IntervalMinutes,
    TimeOnly? DailyTime,
    string TimeZoneId,
    int LookbackDays,
    bool AutoPromote);
