using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Ingestion;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Tools.Schedules;

/// <summary>
/// Lists SP-API ingestion schedules with their next/last run, and lets operators enable, disable,
/// or run them now.
/// </summary>
/// <param name="schedules">Schedule queries.</param>
/// <param name="runs">Run history queries.</param>
/// <param name="scheduleService">Schedule use cases.</param>
/// <param name="connection">Amazon connection state.</param>
public sealed class IndexModel(
    ISyncScheduleRepository schedules,
    ISyncRunRepository runs,
    ISyncScheduleService scheduleService,
    IAmazonConnectionInfo connection) : PageModel
{
    /// <summary>All schedules.</summary>
    public IReadOnlyList<SyncSchedule> Schedules { get; private set; } = [];

    /// <summary>Latest run per schedule id.</summary>
    public IReadOnlyDictionary<int, SyncRun> LatestRuns { get; private set; } = new Dictionary<int, SyncRun>();

    /// <summary>How the app is connected to Amazon.</summary>
    public IAmazonConnectionInfo Connection => connection;

    /// <summary>Loads schedules and their latest runs.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Schedules = await schedules.ListAsync(cancellationToken);
        LatestRuns = await runs.GetLatestByScheduleAsync(cancellationToken);
    }

    /// <summary>Enables or disables a schedule.</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="enabled">Desired state.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostSetEnabledAsync(int id, bool enabled, CancellationToken cancellationToken)
    {
        var result = await scheduleService.SetEnabledAsync(id, enabled, User.Identity!.Name!, cancellationToken);
        TempData[result.IsSuccess ? StatusMessage.Success : StatusMessage.Error] =
            result.IsSuccess ? (enabled ? "Schedule enabled." : "Schedule disabled.") : result.Error;
        return RedirectToPage();
    }

    /// <summary>Queues a run that re-pulls the maximum history Amazon allows (30 days).</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the run history, or back to the list with an error.</returns>
    public async Task<IActionResult> OnPostBackfillAsync(int id, CancellationToken cancellationToken)
    {
        var result = await scheduleService.BackfillAsync(id, SyncScheduleService.MaxLookbackDays, User.Identity!.Name!, cancellationToken);
        if (result.IsFailure)
        {
            TempData[StatusMessage.Error] = result.Error;
            return RedirectToPage();
        }

        TempData[StatusMessage.Success] = $"{SyncScheduleService.MaxLookbackDays}-day backfill queued. Amazon can take several minutes to build a large report.";
        return RedirectToPage("Runs", new { scheduleId = id });
    }

    /// <summary>Queues an immediate run.</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the run history.</returns>
    public async Task<IActionResult> OnPostRunNowAsync(int id, CancellationToken cancellationToken)
    {
        var result = await scheduleService.RunNowAsync(id, User.Identity!.Name!, cancellationToken);
        if (result.IsFailure)
        {
            TempData[StatusMessage.Error] = result.Error;
            return RedirectToPage();
        }

        TempData[StatusMessage.Success] = "Run queued. It appears below within a few seconds; refresh to follow its progress.";
        return RedirectToPage("Runs", new { scheduleId = id });
    }
}
