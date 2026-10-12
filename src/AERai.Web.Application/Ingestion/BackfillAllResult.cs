using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Ingestion;

/// <summary>What queueing a backfill for one schedule did.</summary>
/// <param name="ScheduleName">The schedule's name.</param>
/// <param name="ReportType">What it pulls.</param>
/// <param name="Runs">How many runs were queued (a report-based schedule needs one per 30 days).</param>
/// <param name="AlreadyCovered">How many of those runs repeat a range an earlier backfill already finished.</param>
/// <param name="Note">Something the caller should tell the user, such as a range that had to be shortened; otherwise <see langword="null"/>.</param>
public sealed record BackfillAllScheduleResult(string ScheduleName, AmazonReportType ReportType, int Runs, int AlreadyCovered, string? Note = null);

/// <summary>What queueing "backfill all" did.</summary>
/// <param name="Window">The range that was queued, with the end clamped to now.</param>
/// <param name="Schedules">One entry per schedule that was queued.</param>
public sealed record BackfillAllResult(BackfillWindow Window, IReadOnlyList<BackfillAllScheduleResult> Schedules)
{
    /// <summary>Runs queued across every schedule.</summary>
    public int TotalRuns => Schedules.Sum(s => s.Runs);

    /// <summary>Runs that repeat a range already backfilled.</summary>
    public int TotalAlreadyCovered => Schedules.Sum(s => s.AlreadyCovered);
}
