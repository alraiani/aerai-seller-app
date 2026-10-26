namespace AERai.Web.Application.Ingestion;

/// <summary>Amazon Ads report processing states, reduced to what the sync acts on.</summary>
public enum AdsReportState
{
    /// <summary>Queued or being generated (<c>PENDING</c>, <c>PROCESSING</c>).</summary>
    Pending = 1,

    /// <summary>Generated; the report can be downloaded (<c>COMPLETED</c>).</summary>
    Completed = 2,

    /// <summary>Amazon could not generate it (<c>FAILED</c>).</summary>
    Failed = 3,
}

/// <summary>An Amazon Ads report's processing status.</summary>
/// <param name="State">Processing state.</param>
/// <param name="FailureReason">Amazon's reason when <see cref="AdsReportState.Failed"/>.</param>
public sealed record AdsReportStatus(AdsReportState State, string? FailureReason);
