using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Ingestion;

/// <summary>At-a-glance numbers for the top of the schedule list (always over all active schedules).</summary>
/// <param name="Total">Active schedules.</param>
/// <param name="On">Schedules that run automatically.</param>
/// <param name="Failing">Schedules whose latest run failed.</param>
/// <param name="Running">Schedules with a run in progress.</param>
/// <param name="NextRun">The soonest upcoming run, or <see langword="null"/> if nothing is scheduled.</param>
/// <param name="LatestRun">The most recently started run across all schedules.</param>
public sealed record ScheduleListSummary(
    int Total,
    int On,
    int Failing,
    int Running,
    ScheduleListRow? NextRun,
    ScheduleListRow? LatestRun);
