using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Tests.Ingestion;

public sealed class ScheduleListTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private static SyncSchedule Schedule(int id, string name, AmazonReportType type = AmazonReportType.Orders, bool enabled = true,
        DateTimeOffset? nextRun = null, string? notes = null, string? owner = null, bool deleted = false) => new()
    {
        Id = id, Name = name, ReportType = type, IsEnabled = enabled, Frequency = ScheduleFrequency.Interval, IntervalMinutes = 60,
        TimeZoneId = "UTC", LookbackDays = 7, NextRunAt = nextRun, Notes = notes, OwnerEmail = owner, UpdatedBy = "t",
        DeletedAt = deleted ? Now : null,
    };

    private static SyncRun Run(int scheduleId, SyncRunStatus status, DateTimeOffset startedAt) =>
        new() { SyncScheduleId = scheduleId, Status = status, StartedAt = startedAt, TriggeredBy = "t" };

    private static readonly SyncSchedule[] Schedules =
    [
        Schedule(1, "Orders hourly", nextRun: Now.AddMinutes(30), owner: "pat@example.com"),
        Schedule(2, "FBA daily", AmazonReportType.FbaInventory, nextRun: Now.AddHours(5), notes: "Feeds the reorder sheet"),
        Schedule(3, "Settlements", AmazonReportType.Settlements, enabled: false),
        Schedule(4, "Old orders", deleted: true),
    ];

    private static readonly Dictionary<int, SyncRun> LatestRuns = new()
    {
        [1] = Run(1, SyncRunStatus.Failed, Now.AddMinutes(-30)),
        [2] = Run(2, SyncRunStatus.Succeeded, Now.AddHours(-19)),
    };

    private static List<int> Ids(ScheduleListFilter filter) =>
        ScheduleList.Apply(Schedules, LatestRuns, filter).Select(r => r.Schedule.Id).ToList();

    [Fact]
    public void Apply_Default_ShowsActiveSchedulesByName()
    {
        Assert.Equal([2, 1, 3], Ids(new ScheduleListFilter()));
    }

    [Fact]
    public void Apply_DeletedView_ShowsOnlyDeleted()
    {
        Assert.Equal([4], Ids(new ScheduleListFilter(View: ScheduleView.Deleted)));
    }

    [Theory]
    [InlineData("hourly", 1)]
    [InlineData("REORDER", 2)]          // Notes.
    [InlineData("pat@", 1)]             // Owner.
    [InlineData("settlements", 3)]      // Report type.
    public void Apply_Query_MatchesNameNotesOwnerAndReport(string query, int expectedId)
    {
        Assert.Equal([expectedId], Ids(new ScheduleListFilter(Query: query)));
    }

    [Fact]
    public void Apply_Filters_Combine()
    {
        Assert.Equal([3], Ids(new ScheduleListFilter(Status: ScheduleStatusFilter.Off)));
        Assert.Equal([2], Ids(new ScheduleListFilter(ReportType: AmazonReportType.FbaInventory)));
        Assert.Equal([1], Ids(new ScheduleListFilter(LastRun: LastRunFilter.Failed)));
        Assert.Equal([3], Ids(new ScheduleListFilter(LastRun: LastRunFilter.Never)));
        Assert.Equal([1], Ids(new ScheduleListFilter(OwnerEmail: "PAT@example.com")));
        Assert.Empty(Ids(new ScheduleListFilter(Status: ScheduleStatusFilter.Off, LastRun: LastRunFilter.Failed)));
    }

    [Fact]
    public void Apply_SortByNextRun_SoonestFirstAndUnscheduledLastEitherWay()
    {
        Assert.Equal([1, 2, 3], Ids(new ScheduleListFilter(Sort: ScheduleSort.NextRun)));
        Assert.Equal([2, 1, 3], Ids(new ScheduleListFilter(Sort: ScheduleSort.NextRun, Descending: true)));
    }

    [Fact]
    public void Apply_SortByLastRun_MostRecentFirstAndNeverRunLast()
    {
        Assert.Equal([1, 2, 3], Ids(new ScheduleListFilter(Sort: ScheduleSort.LastRun)));
    }

    [Fact]
    public void Apply_SortByName_Descending()
    {
        Assert.Equal([3, 1, 2], Ids(new ScheduleListFilter(Descending: true)));
    }

    [Fact]
    public void Summarize_CountsActiveSchedulesOnly()
    {
        var summary = ScheduleList.Summarize(Schedules, LatestRuns);

        Assert.Equal(3, summary.Total);
        Assert.Equal(2, summary.On);
        Assert.Equal(1, summary.Failing);
        Assert.Equal(1, summary.NextRun?.Schedule.Id);
        Assert.Equal(1, summary.LatestRun?.Schedule.Id);
    }
}
