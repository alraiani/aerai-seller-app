namespace AERai.Web.Application.Ingestion;

/// <summary>Which schedules the list shows.</summary>
public enum ScheduleView
{
    /// <summary>Schedules in use.</summary>
    Active,

    /// <summary>Deleted schedules, which can be restored.</summary>
    Deleted,
}
