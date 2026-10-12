namespace AERai.Web.Application.Ingestion;

/// <summary>Filters schedules by the outcome of their latest run.</summary>
public enum LastRunFilter
{
    /// <summary>No filter.</summary>
    All,

    /// <summary>Latest run failed.</summary>
    Failed,

    /// <summary>Latest run succeeded or found no new data.</summary>
    Succeeded,

    /// <summary>A run is in progress.</summary>
    Running,

    /// <summary>Never run.</summary>
    Never,
}
