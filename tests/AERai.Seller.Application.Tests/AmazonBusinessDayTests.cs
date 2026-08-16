using AERai.Seller.Domain;

namespace AERai.Seller.Application.Tests;

public class AmazonBusinessDayTests
{
    [Fact]
    public void DateOf_UtcInstantEarlyInUtcDay_IsStillPreviousPacificDay()
    {
        // 03:00 UTC on Aug 2 is 20:00 Pacific (PDT, UTC-7) on Aug 1 — still "yesterday" in Pacific.
        var instant = new DateTimeOffset(2026, 8, 2, 3, 0, 0, TimeSpan.Zero);

        var date = AmazonBusinessDay.DateOf(instant);

        Assert.Equal(new DateOnly(2026, 8, 1), date);
    }

    [Fact]
    public void DateOf_UtcInstantAfterPacificMidnight_IsCurrentPacificDay()
    {
        // 08:00 UTC on Aug 2 is 01:00 Pacific (PDT) on Aug 2 — already "today" in Pacific.
        var instant = new DateTimeOffset(2026, 8, 2, 8, 0, 0, TimeSpan.Zero);

        var date = AmazonBusinessDay.DateOf(instant);

        Assert.Equal(new DateOnly(2026, 8, 2), date);
    }

    [Fact]
    public void StartOfDayUtc_RoundTripsWithDateOf()
    {
        var date = new DateOnly(2026, 8, 2);

        var startUtc = AmazonBusinessDay.StartOfDayUtc(date);

        Assert.Equal(date, AmazonBusinessDay.DateOf(startUtc));
        Assert.Equal(date.AddDays(-1), AmazonBusinessDay.DateOf(startUtc.AddSeconds(-1)));
    }

    [Fact]
    public void StartOfDayUtc_UsesPacificOffset_NotUtc()
    {
        // PDT (summer) is UTC-7, so Aug 2 Pacific midnight is Aug 2 07:00 UTC, not Aug 2 00:00 UTC.
        var startUtc = AmazonBusinessDay.StartOfDayUtc(new DateOnly(2026, 8, 2));

        Assert.Equal(new DateTimeOffset(2026, 8, 2, 7, 0, 0, TimeSpan.Zero), startUtc);
    }

    [Fact]
    public void TodayIn_MatchesDateOf()
    {
        var now = new DateTimeOffset(2026, 8, 2, 19, 47, 0, TimeSpan.Zero);

        Assert.Equal(AmazonBusinessDay.DateOf(now), AmazonBusinessDay.TodayIn(now));
    }
}
