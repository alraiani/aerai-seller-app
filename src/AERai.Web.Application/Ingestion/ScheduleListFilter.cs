using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Ingestion;

/// <summary>Search, filter, and sort options for the Amazon sync schedule list.</summary>
/// <param name="Query">Free text matched against name, report, notes, and owner.</param>
/// <param name="View">Active or deleted schedules.</param>
/// <param name="Status">On/off filter.</param>
/// <param name="ReportType">Only this report type, or all.</param>
/// <param name="LastRun">Filter by the latest run's outcome.</param>
/// <param name="OwnerEmail">Only schedules owned by this user, or all.</param>
/// <param name="Sort">Sort column.</param>
/// <param name="Descending">Reverse the sort.</param>
public sealed record ScheduleListFilter(
    string? Query = null,
    ScheduleView View = ScheduleView.Active,
    ScheduleStatusFilter Status = ScheduleStatusFilter.All,
    AmazonReportType? ReportType = null,
    LastRunFilter LastRun = LastRunFilter.All,
    string? OwnerEmail = null,
    ScheduleSort Sort = ScheduleSort.Name,
    bool Descending = false);
