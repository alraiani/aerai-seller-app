namespace AERai.Web.Application.Ingestion;

/// <summary>An action applied to several schedules at once from the Amazon sync list.</summary>
public enum BulkScheduleAction
{
    /// <summary>Turn automatic runs off (manual runs still work).</summary>
    Disable,

    /// <summary>Turn automatic runs on.</summary>
    Enable,

    /// <summary>Soft-delete.</summary>
    Delete,

    /// <summary>Restore deleted schedules (they come back turned off).</summary>
    Restore,
}
