using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Dashboard;

/// <summary>Freshness and health of one report type's Amazon sync.</summary>
/// <param name="ReportType">Report type.</param>
/// <param name="IsScheduled">Whether any schedule for it is enabled.</param>
/// <param name="LastSuccessAt">When a run last succeeded (or found no new data).</param>
/// <param name="LastRunAt">When the latest run started.</param>
/// <param name="LastStatus">Outcome of the latest run.</param>
/// <param name="LastMessage">Message of the latest run.</param>
public sealed record SyncGlance(
    AmazonReportType ReportType,
    bool IsScheduled,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastRunAt,
    SyncRunStatus? LastStatus,
    string? LastMessage);
