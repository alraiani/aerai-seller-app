using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Tests.Ingestion;

public sealed class ScheduleCalculatorTests
{
    private static SyncSchedule Daily(int hour, int minute, string zone = "America/New_York") => new()
    {
        Name = "t", Frequency = ScheduleFrequency.Daily, DailyTime = new TimeOnly(hour, minute), TimeZoneId = zone, UpdatedBy = "t",
    };

    private static DateTimeOffset Utc(int y, int mo, int d, int h, int mi) => new(y, mo, d, h, mi, 0, TimeSpan.Zero);

    [Fact]
    public void NextRunAfter_Interval_AddsMinutes()
    {
        var schedule = new SyncSchedule { Name = "t", Frequency = ScheduleFrequency.Interval, IntervalMinutes = 90, TimeZoneId = "UTC", UpdatedBy = "t" };

        Assert.Equal(Utc(2026, 10, 2, 13, 30), ScheduleCalculator.NextRunAfter(schedule, Utc(2026, 10, 2, 12, 0)));
    }

    [Fact]
    public void NextRunAfter_DailyLaterToday_RunsToday()
    {
        // 06:00 EDT (UTC-4) on Oct 2 = 10:00Z; now is 09:00Z (05:00 local).
        Assert.Equal(Utc(2026, 10, 2, 10, 0), ScheduleCalculator.NextRunAfter(Daily(6, 0), Utc(2026, 10, 2, 9, 0)));
    }

    [Fact]
    public void NextRunAfter_DailyAlreadyPassed_RunsTomorrow()
    {
        Assert.Equal(Utc(2026, 10, 3, 10, 0), ScheduleCalculator.NextRunAfter(Daily(6, 0), Utc(2026, 10, 2, 10, 0)));
    }

    [Fact]
    public void NextRunAfter_DailyUsesLocalDateNotUtcDate()
    {
        // 02:00Z Oct 3 is still 22:00 Oct 2 in New York, so a 23:00 local run is later that same local day.
        Assert.Equal(Utc(2026, 10, 3, 3, 0), ScheduleCalculator.NextRunAfter(Daily(23, 0), Utc(2026, 10, 3, 2, 0)));
    }

    [Fact]
    public void NextRunAfter_SpringForwardGap_RunsAtFirstValidTime()
    {
        // 2026-03-08 02:30 doesn't exist in New York (clocks jump 02:00 → 03:00). Runs at 03:30 EDT = 07:30Z.
        Assert.Equal(Utc(2026, 3, 8, 7, 30), ScheduleCalculator.NextRunAfter(Daily(2, 30), Utc(2026, 3, 8, 5, 0)));
    }

    [Fact]
    public void NextRunAfter_FallBackOverlap_RunsOnceAtFirstOccurrence()
    {
        // 2026-11-01 01:30 happens twice in New York; the first (EDT, UTC-4) is 05:30Z.
        var first = ScheduleCalculator.NextRunAfter(Daily(1, 30), Utc(2026, 11, 1, 4, 0));
        Assert.Equal(Utc(2026, 11, 1, 5, 30), first);

        // After the first occurrence, the repeated 01:30 (EST) is skipped; next is the following day (06:30Z).
        Assert.Equal(Utc(2026, 11, 2, 6, 30), ScheduleCalculator.NextRunAfter(Daily(1, 30), first));
    }

    [Fact]
    public void NextRunAfter_Daily_IsAlwaysStrictlyAfterReference()
    {
        var at = Utc(2026, 10, 2, 10, 0); // exactly 06:00 local
        Assert.True(ScheduleCalculator.NextRunAfter(Daily(6, 0), at) > at);
    }
}
