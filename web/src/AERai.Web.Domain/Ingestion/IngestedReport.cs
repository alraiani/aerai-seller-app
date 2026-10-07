namespace AERai.Web.Domain.Ingestion;

/// <summary>
/// Records each Amazon report already ingested, so list-based report types (settlements) are never
/// ingested twice even when runs overlap or are repeated.
/// </summary>
public sealed class IngestedReport
{
    /// <summary>Amazon report id (natural key).</summary>
    public required string AmazonReportId { get; set; }

    /// <summary>Report type.</summary>
    public AmazonReportType ReportType { get; set; }

    /// <summary>Staging batch created from it.</summary>
    public long ImportBatchId { get; set; }

    /// <summary>Run that ingested it.</summary>
    public long SyncRunId { get; set; }

    /// <summary>When it was ingested.</summary>
    public DateTimeOffset IngestedAt { get; set; }
}
