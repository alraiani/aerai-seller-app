using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Ingestion;

/// <summary>One schedule on the list, with its latest run.</summary>
/// <param name="Schedule">The schedule.</param>
/// <param name="LastRun">Its latest run, or <see langword="null"/> if never run.</param>
public sealed record ScheduleListRow(SyncSchedule Schedule, SyncRun? LastRun);
