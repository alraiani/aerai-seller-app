using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Tests.Ingestion;

public sealed class ScheduleCalculatorTests
{
    private static SyncSchedule Daily(int hour, int minute, string zone = "America/New_York") => new()
    {
        Name = "t", MarketplaceId = "ATVPDKIKX0DER", Frequency = ScheduleFrequency.Daily, DailyTime = new TimeOnly(hour, minute), TimeZoneId = zone, UpdatedBy = "t",
    };

    private static DateTimeOffset Utc(int y, int mo, int d, int h, int mi) => new(y, mo, d, h, mi, 0, TimeSpan.Zero);

    [Fact]
    public void NextRunAfter_Interval_AddsMinutes()
    {
        var schedule = new SyncSchedule { Name = "t", MarketplaceId = "ATVPDKIKX0DER", Frequency = ScheduleFrequency.Interval, IntervalMinutes = 90, TimeZoneId = "UTC", UpdatedBy = "t" };

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

    private static SyncSchedule Interval(int minutes, TimeOnly from, TimeOnly until, string zone = "America/New_York") => new()
    {
        Name = "t", MarketplaceId = "ATVPDKIKX0DER", Frequency = ScheduleFrequency.Interval, IntervalMinutes = minutes,
        ActiveFrom = from, ActiveUntil = until, TimeZoneId = zone, UpdatedBy = "t",
    };

    [Fact]
    public void NextRunAfter_IntervalInsideActiveHours_AddsMinutes()
    {
        // 11:00 EDT = 15:00Z; four hours later is 15:00 EDT, still inside 7 AM–9 PM.
        Assert.Equal(Utc(2026, 10, 2, 19, 0), ScheduleCalculator.NextRunAfter(Interval(240, new(7, 0), new(21, 0)), Utc(2026, 10, 2, 15, 0)));
    }

    [Fact]
    public void NextRunAfter_IntervalPastActiveHours_WaitsForTomorrowsStart()
    {
        // 19:00 EDT + 4 h = 23:00, after 9 PM, so the next run is 7:00 EDT next day (11:00Z).
        Assert.Equal(Utc(2026, 10, 3, 11, 0), ScheduleCalculator.NextRunAfter(Interval(240, new(7, 0), new(21, 0)), Utc(2026, 10, 2, 23, 0)));
    }

    [Fact]
    public void NextRunAfter_EnabledBeforeActiveHours_RunsAtTodaysStart()
    {
        // 03:00 EDT + 4 h = 07:00 exactly, the (inclusive) start; 02:00 EDT + 4 h = 06:00, before it.
        Assert.Equal(Utc(2026, 10, 2, 11, 0), ScheduleCalculator.NextRunAfter(Interval(240, new(7, 0), new(21, 0)), Utc(2026, 10, 2, 7, 0)));
        Assert.Equal(Utc(2026, 10, 2, 11, 0), ScheduleCalculator.NextRunAfter(Interval(240, new(7, 0), new(21, 0)), Utc(2026, 10, 2, 6, 0)));
    }

    [Fact]
    public void NextRunAfter_ActiveHoursPastMidnight_WrapAround()
    {
        var overnight = Interval(60, new(22, 0), new(2, 0), "UTC");

        Assert.Equal(Utc(2026, 10, 3, 1, 0), ScheduleCalculator.NextRunAfter(overnight, Utc(2026, 10, 3, 0, 0)));
        Assert.Equal(Utc(2026, 10, 3, 22, 0), ScheduleCalculator.NextRunAfter(overnight, Utc(2026, 10, 3, 1, 30)));
    }

    [Fact]
    public void NextRunAfter_ActiveHoursAcrossSpringForward_StartsAtLocalStart()
    {
        // US DST starts 2027-03-14: 7:00 is EDT (UTC-4) that morning, though the evening before was EST (UTC-5).
        Assert.Equal(Utc(2027, 3, 14, 11, 0), ScheduleCalculator.NextRunAfter(Interval(240, new(7, 0), new(21, 0)), Utc(2027, 3, 14, 1, 0)));
    }

    [Theory]
    [InlineData(7, 0, true)]
    [InlineData(20, 59, true)]
    [InlineData(21, 0, false)]
    [InlineData(6, 59, false)]
    public void IsActive_DaytimeWindow_IncludesStartExcludesEnd(int hour, int minute, bool expected) =>
        Assert.Equal(expected, ScheduleCalculator.IsActive(new TimeOnly(hour, minute), new TimeOnly(7, 0), new TimeOnly(21, 0)));
}
