using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Ingestion;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Tools.Schedules;

/// <summary>
/// Ingestion run history, newest first, optionally filtered to one schedule.
/// </summary>
/// <param name="runs">Run history queries.</param>
/// <param name="schedules">Schedule queries (for names).</param>
public sealed class RunsModel(ISyncRunRepository runs, ISyncScheduleRepository schedules) : ListPageModel
{
    /// <summary>Optional schedule filter from <c>?scheduleId=</c>.</summary>
    [BindProperty(SupportsGet = true)]
    public int? ScheduleId { get; set; }

    /// <summary>The current page of runs.</summary>
    public PagedResult<SyncRun> Runs { get; private set; } = default!;

    /// <summary>Schedule names by id.</summary>
    public IReadOnlyDictionary<int, string> ScheduleNames { get; private set; } = new Dictionary<int, string>();

    /// <summary>Whether any run on this page is still in progress (the page then refreshes itself).</summary>
    public bool AnyRunning => Runs.Items.Any(r => r.Status == SyncRunStatus.Running);

    /// <summary>Loads the page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Runs = await runs.ListAsync(ScheduleId, ToPageRequest(), cancellationToken);
        // Include deleted schedules so their history stays labeled.
        ScheduleNames = (await schedules.ListIncludingDeletedAsync(cancellationToken))
            .ToDictionary(s => s.Id, s => s.IsDeleted ? $"{s.Name} (deleted)" : s.Name);
    }
}
