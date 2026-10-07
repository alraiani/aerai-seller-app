namespace AERai.Web.Application.Ingestion;

/// <summary>Sort columns for the schedule list.</summary>
public enum ScheduleSort
{
    /// <summary>Schedule name.</summary>
    Name,

    /// <summary>Report type, then name.</summary>
    Report,

    /// <summary>Next scheduled run, soonest first; schedules with no next run last.</summary>
    NextRun,

    /// <summary>Latest run start, most recent first; never-run schedules last.</summary>
    LastRun,
}
