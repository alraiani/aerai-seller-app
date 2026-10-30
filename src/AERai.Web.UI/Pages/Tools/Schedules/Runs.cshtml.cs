using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Domain.Ingestion;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Tools.Schedules;

/// <summary>
/// Ingestion run history, newest first, optionally filtered to one schedule.
/// </summary>
/// <param name="runs">Run history queries.</param>
/// <param name="schedules">Schedule queries (for names).</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
/// <param name="ingestion">Stops runs that haven't finished.</param>
/// <param name="clock">Clock for relative times.</param>
public sealed class RunsModel(ISyncRunRepository runs, ISyncScheduleRepository schedules, ICurrentMarketplace currentMarketplace, IReportIngestionService ingestion, TimeProvider clock) : ListPageModel
{
    /// <summary>Now, for "3 h ago".</summary>
    public DateTimeOffset Now { get; } = clock.GetUtcNow();

    /// <summary>The marketplace's time zone (IANA id), for exact start times.</summary>
    public string TimeZoneId { get; private set; } = "UTC";

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
    /// <returns>The page, or only its list for a partial reload.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        TimeZoneId = marketplace.TimeZoneId;
        Runs = await runs.ListAsync(marketplace.MarketplaceId, ScheduleId, ToPageRequest(), cancellationToken);
        // Include deleted schedules so their history stays labeled.
        ScheduleNames = (await schedules.ListIncludingDeletedAsync(cancellationToken))
            .ToDictionary(s => s.Id, s => s.IsDeleted ? $"{s.Name} (deleted)" : s.Name);
        return this.PageOrFragment("_RunsBody");
    }

    /// <summary>Stops a run that hasn't finished (waiting on Amazon, or working right now).</summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the history.</returns>
    public async Task<IActionResult> OnPostStopAsync(long runId, CancellationToken cancellationToken)
    {
        var marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var result = await ingestion.StopAsync(runId, marketplace.MarketplaceId, User.Identity!.Name!, cancellationToken); // Non-null: Tools requires an authenticated Operator.
        TempData[result.IsSuccess ? StatusMessage.Success : StatusMessage.Error] = result.IsSuccess ? result.Value : result.Error;
        return RedirectToPage(new { scheduleId = ScheduleId });
    }
}
