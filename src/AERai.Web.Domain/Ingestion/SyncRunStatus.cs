namespace AERai.Web.Domain.Ingestion;

/// <summary>Outcome of a <see cref="SyncRun"/>.</summary>
public enum SyncRunStatus
{
    /// <summary>In progress.</summary>
    Running = 1,

    /// <summary>Report(s) downloaded, landed, and staged (and promoted, if enabled).</summary>
    Succeeded = 2,

    /// <summary>Completed without error, but Amazon had no data for the window.</summary>
    NoData = 3,

    /// <summary>Stopped by an error; see <see cref="SyncRun.Message"/>.</summary>
    Failed = 4,
}
