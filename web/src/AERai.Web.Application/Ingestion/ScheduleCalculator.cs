using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Computes when a <see cref="SyncSchedule"/> should next run. Pure and deterministic, so the
/// daylight-saving edge cases are unit-tested.
/// </summary>
public static class ScheduleCalculator
{
    /// <summary>Gets the first run time strictly after <paramref name="after"/>.</summary>
    /// <param name="schedule">The schedule (its frequency fields must be valid).</param>
    /// <param name="after">Reference time, typically "now".</param>
    /// <returns>The next run time, in UTC.</returns>
    /// <exception cref="InvalidOperationException">The schedule's frequency settings are incomplete.</exception>
    public static DateTimeOffset NextRunAfter(SyncSchedule schedule, DateTimeOffset after)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        return schedule.Frequency switch
        {
            ScheduleFrequency.Interval => NextInterval(schedule, after),
            ScheduleFrequency.Daily => NextDaily(
                schedule.DailyTime ?? throw new InvalidOperationException("Daily schedules need DailyTime."),
                TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId),
                after),
            _ => throw new InvalidOperationException($"Unknown frequency '{schedule.Frequency}'."),
        };
    }

    /// <summary>
    /// One interval after <paramref name="after"/>; if that falls outside the schedule's active
    /// window, the start of the next window instead (so the first run of the day is at its start).
    /// </summary>
    private static DateTimeOffset NextInterval(SyncSchedule schedule, DateTimeOffset after)
    {
        var candidate = after.ToUniversalTime().AddMinutes(
            schedule.IntervalMinutes ?? throw new InvalidOperationException("Interval schedules need IntervalMinutes."));
        if (schedule.ActiveFrom is not { } from || schedule.ActiveUntil is not { } until)
        {
            return candidate;
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
        if (IsActive(TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(candidate, zone).DateTime), from, until))
        {
            return candidate;
        }

        // The window opens at most a day away; NextDaily never returns a time before the candidate.
        return NextDaily(from, zone, candidate.AddTicks(-1));
    }

    /// <summary>Whether a local time is inside a window; an end before the start wraps past midnight.</summary>
    /// <param name="time">Local time of day.</param>
    /// <param name="from">Window start (inclusive).</param>
    /// <param name="until">Window end (exclusive).</param>
    /// <returns><see langword="true"/> when inside.</returns>
    public static bool IsActive(TimeOnly time, TimeOnly from, TimeOnly until) =>
        from < until ? time >= from && time < until : time >= from || time < until;

    /// <summary>Next occurrence of a local wall-clock time in a time zone.</summary>
    private static DateTimeOffset NextDaily(TimeOnly time, TimeZoneInfo zone, DateTimeOffset after)
    {
        var localNow = TimeZoneInfo.ConvertTime(after, zone);
        var date = DateOnly.FromDateTime(localNow.DateTime);

        for (var day = 0; day < 3; day++)
        {
            var candidate = ToUtc(date.AddDays(day).ToDateTime(time), zone);
            if (candidate > after)
            {
                return candidate;
            }
        }

        // Unreachable: within three calendar days a later occurrence always exists.
        throw new InvalidOperationException("Could not compute the next daily run.");
    }

    /// <summary>
    /// Converts a local wall-clock time to UTC, resolving daylight-saving transitions the way people
    /// expect: a time skipped by spring-forward runs at the first valid moment after the gap, and a
    /// time repeated by fall-back runs once, at its first occurrence.
    /// </summary>
    private static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            // Spring-forward gap: move ahead by the DST delta (typically one hour).
            var delta = zone.GetAdjustmentRules()
                .Where(r => r.DateStart <= local && r.DateEnd >= local)
                .Select(r => r.DaylightDelta)
                .DefaultIfEmpty(TimeSpan.FromHours(1))
                .First();
            local = local.Add(delta);
        }

        if (zone.IsAmbiguousTime(local))
        {
            // Fall-back overlap: the larger offset is the earlier (daylight-time) occurrence.
            var offset = zone.GetAmbiguousTimeOffsets(local).Max();
            return new DateTimeOffset(local, offset).ToUniversalTime();
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }
}
