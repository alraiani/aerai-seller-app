using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Search, filter, sort, and summary rules for the Amazon sync schedule list. In memory on purpose:
/// a seller has tens of schedules, not thousands, and this keeps the rules unit-testable.
/// </summary>
public static class ScheduleList
{
    /// <summary>Joins schedules to their latest runs and applies a filter.</summary>
    /// <param name="schedules">Schedules, including deleted ones when the filter asks for them.</param>
    /// <param name="latestRuns">Latest run per schedule id.</param>
    /// <param name="filter">Search, filter, and sort options.</param>
    /// <returns>Matching rows in display order.</returns>
    public static IReadOnlyList<ScheduleListRow> Apply(
        IEnumerable<SyncSchedule> schedules,
        IReadOnlyDictionary<int, SyncRun> latestRuns,
        ScheduleListFilter filter)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        ArgumentNullException.ThrowIfNull(latestRuns);
        ArgumentNullException.ThrowIfNull(filter);

        var query = filter.Query?.Trim();
        var rows = schedules
            .Where(s => s.IsDeleted == (filter.View == ScheduleView.Deleted))
            .Select(s => new ScheduleListRow(s, latestRuns.GetValueOrDefault(s.Id)))
            .Where(r => string.IsNullOrEmpty(query) || Matches(r.Schedule, query))
            .Where(r => filter.Status switch
            {
                ScheduleStatusFilter.On => r.Schedule.IsEnabled,
                ScheduleStatusFilter.Off => !r.Schedule.IsEnabled,
                _ => true,
            })
            .Where(r => filter.ReportType is not { } type || r.Schedule.ReportType == type)
            .Where(r => filter.LastRun switch
            {
                LastRunFilter.Failed => r.LastRun?.Status == SyncRunStatus.Failed,
                LastRunFilter.Succeeded => r.LastRun?.Status is SyncRunStatus.Succeeded or SyncRunStatus.NoData,
                LastRunFilter.Running => r.LastRun?.Status == SyncRunStatus.Running,
                LastRunFilter.Never => r.LastRun is null,
                _ => true,
            })
            .Where(r => string.IsNullOrEmpty(filter.OwnerEmail)
                || string.Equals(r.Schedule.OwnerEmail, filter.OwnerEmail, StringComparison.OrdinalIgnoreCase));

        return Sort(rows, filter.Sort, filter.Descending).ToList();
    }

    /// <summary>Builds the summary tiles over all active schedules, ignoring any filter.</summary>
    /// <param name="schedules">Schedules (deleted ones are skipped).</param>
    /// <param name="latestRuns">Latest run per schedule id.</param>
    /// <returns>The summary.</returns>
    public static ScheduleListSummary Summarize(IEnumerable<SyncSchedule> schedules, IReadOnlyDictionary<int, SyncRun> latestRuns)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        ArgumentNullException.ThrowIfNull(latestRuns);

        var rows = schedules
            .Where(s => !s.IsDeleted)
            .Select(s => new ScheduleListRow(s, latestRuns.GetValueOrDefault(s.Id)))
            .ToList();

        return new ScheduleListSummary(
            Total: rows.Count,
            On: rows.Count(r => r.Schedule.IsEnabled),
            Failing: rows.Count(r => r.LastRun?.Status == SyncRunStatus.Failed),
            Running: rows.Count(r => r.LastRun?.Status == SyncRunStatus.Running),
            NextRun: rows.Where(r => r.Schedule is { IsEnabled: true, NextRunAt: not null }).MinBy(r => r.Schedule.NextRunAt),
            LatestRun: rows.Where(r => r.LastRun is not null).MaxBy(r => r.LastRun!.StartedAt));
    }

    private static bool Matches(SyncSchedule schedule, string query) =>
        Contains(schedule.Name, query)
        || Contains(schedule.ReportType.ToString(), query)
        || Contains(schedule.Notes, query)
        || Contains(schedule.OwnerEmail, query);

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    private static IEnumerable<ScheduleListRow> Sort(IEnumerable<ScheduleListRow> rows, ScheduleSort sort, bool descending)
    {
        // Rows without a value (never run / no next run) stay at the bottom in both directions.
        var ordered = sort switch
        {
            ScheduleSort.Report => Order(rows, r => r.Schedule.ReportType.ToString(), descending)
                .ThenBy(r => r.Schedule.Name, StringComparer.OrdinalIgnoreCase),
            ScheduleSort.NextRun => rows.OrderBy(r => r.Schedule.NextRunAt is null)
                .ThenBy(r => r.Schedule.NextRunAt, descending),
            ScheduleSort.LastRun => rows.OrderBy(r => r.LastRun is null)
                .ThenBy(r => r.LastRun?.StartedAt, !descending),
            _ => Order(rows, r => r.Schedule.Name, descending),
        };
        return ordered;
    }

    private static IOrderedEnumerable<ScheduleListRow> Order(IEnumerable<ScheduleListRow> rows, Func<ScheduleListRow, string> key, bool descending) =>
        descending
            ? rows.OrderByDescending(key, StringComparer.OrdinalIgnoreCase)
            : rows.OrderBy(key, StringComparer.OrdinalIgnoreCase);

    private static IOrderedEnumerable<ScheduleListRow> ThenBy<TKey>(this IOrderedEnumerable<ScheduleListRow> rows, Func<ScheduleListRow, TKey> key, bool descending) =>
        descending ? rows.ThenByDescending(key) : rows.ThenBy(key);
}
