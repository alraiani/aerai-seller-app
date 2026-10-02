namespace AERai.Web.Domain.Ingestion;

/// <summary>What started a <see cref="SyncRun"/>.</summary>
public enum SyncTrigger
{
    /// <summary>The background scheduler, because the schedule was due.</summary>
    Scheduled = 1,

    /// <summary>A user clicked "Run now".</summary>
    Manual = 2,
}
