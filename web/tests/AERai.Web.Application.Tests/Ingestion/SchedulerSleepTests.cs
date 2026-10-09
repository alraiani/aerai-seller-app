using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Tests.Ingestion;

public sealed class SchedulerSleepTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan MaxSleep = TimeSpan.FromHours(6);

    [Fact]
    public void Until_NothingScheduled_SleepsTheMaximum() =>
        Assert.Equal(MaxSleep, SchedulerSleep.Until(Now, MaxSleep, null, null));

    [Fact]
    public void Until_SeveralEvents_SleepsUntilTheEarliest() =>
        Assert.Equal(TimeSpan.FromMinutes(20), SchedulerSleep.Until(Now, MaxSleep, Now.AddHours(2), Now.AddMinutes(20), null));

    [Fact]
    public void Until_EventBeyondTheCap_SleepsTheMaximum() =>
        Assert.Equal(MaxSleep, SchedulerSleep.Until(Now, MaxSleep, Now.AddDays(1)));

    [Fact]
    public void Until_OverdueEvent_DoesNotSleep() =>
        Assert.Equal(TimeSpan.Zero, SchedulerSleep.Until(Now, MaxSleep, Now.AddMinutes(-5)));
}
