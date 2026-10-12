namespace AERai.Web.Application.Ingestion;

/// <summary>Filters schedules by their on/off switch.</summary>
public enum ScheduleStatusFilter
{
    /// <summary>No filter.</summary>
    All,

    /// <summary>Runs automatically.</summary>
    On,

    /// <summary>Manual runs only.</summary>
    Off,
}
