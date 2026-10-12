namespace AERai.Web.Domain.Ingestion;

/// <summary>How a <see cref="SyncSchedule"/> repeats.</summary>
public enum ScheduleFrequency
{
    /// <summary>Every <see cref="SyncSchedule.IntervalMinutes"/> minutes.</summary>
    Interval = 1,

    /// <summary>Once a day at <see cref="SyncSchedule.DailyTime"/> in <see cref="SyncSchedule.TimeZoneId"/>.</summary>
    Daily = 2,
}
