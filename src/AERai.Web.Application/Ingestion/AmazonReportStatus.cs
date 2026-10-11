namespace AERai.Web.Application.Ingestion;

/// <summary>SP-API report processing states.</summary>
public enum AmazonProcessingStatus
{
    /// <summary>Waiting to be processed.</summary>
    InQueue = 1,

    /// <summary>Being generated.</summary>
    InProgress = 2,

    /// <summary>Generated; the document can be downloaded.</summary>
    Done = 3,

    /// <summary>Cancelled — for on-demand reports this usually means there was no data.</summary>
    Cancelled = 4,

    /// <summary>Generation failed on Amazon's side.</summary>
    Fatal = 5,
}

/// <summary>A report's processing status.</summary>
/// <param name="Status">Processing state.</param>
/// <param name="ReportDocumentId">Document id when <see cref="AmazonProcessingStatus.Done"/>.</param>
public sealed record AmazonReportStatus(AmazonProcessingStatus Status, string? ReportDocumentId);

/// <summary>A completed report available for download.</summary>
/// <param name="ReportId">Amazon report id.</param>
/// <param name="ReportDocumentId">Document id.</param>
/// <param name="CreatedAt">When Amazon created the report.</param>
public sealed record AvailableAmazonReport(string ReportId, string ReportDocumentId, DateTimeOffset CreatedAt);
