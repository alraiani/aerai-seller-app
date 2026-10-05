namespace AERai.Web.Application.Ingestion;

/// <summary>A user's request to run a schedule immediately.</summary>
/// <param name="ScheduleId">Schedule to run.</param>
/// <param name="RequestedBy">User name, recorded on the run.</param>
/// <param name="BackfillDays">
/// When set, re-pulls this many days of history instead of continuing from the last successful run.
/// </param>
public sealed record ManualRunRequest(int ScheduleId, string RequestedBy, int? BackfillDays = null);
