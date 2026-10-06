using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Security;
using AERai.Web.Domain.Ingestion;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Tools.Schedules;

/// <summary>
/// The Amazon sync page: SP-API status, summary tiles, and a searchable, filterable, sortable list of
/// schedules with their actions (toggle, run, backfill, duplicate, delete, restore) and a global
/// pause switch.
/// </summary>
/// <param name="schedules">Schedule queries.</param>
/// <param name="runs">Run history queries.</param>
/// <param name="syncSettings">Global pause state.</param>
/// <param name="scheduleService">Schedule use cases.</param>
/// <param name="connection">Amazon connection state.</param>
/// <param name="tester">Amazon connection test.</param>
/// <param name="identity">User list for the owner filter.</param>
/// <param name="clock">Clock for relative times.</param>
public sealed class IndexModel(
    ISyncScheduleRepository schedules,
    ISyncRunRepository runs,
    ISyncSettingsRepository syncSettings,
    ISyncScheduleService scheduleService,
    IAmazonConnectionInfo connection,
    IAmazonConnectionTester tester,
    IIdentityService identity,
    TimeProvider clock) : PageModel
{
    /// <summary>Free-text search (<c>?q=</c>).</summary>
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    /// <summary>Active or deleted schedules (<c>?view=</c>).</summary>
    [BindProperty(SupportsGet = true)]
    public ScheduleView View { get; set; }

    /// <summary>On/off filter (<c>?status=</c>).</summary>
    [BindProperty(SupportsGet = true)]
    public ScheduleStatusFilter Status { get; set; }

    /// <summary>Report type filter (<c>?report=</c>).</summary>
    [BindProperty(SupportsGet = true, Name = "report")]
    public AmazonReportType? ReportType { get; set; }

    /// <summary>Latest-run filter (<c>?last=</c>).</summary>
    [BindProperty(SupportsGet = true, Name = "last")]
    public LastRunFilter LastRun { get; set; }

    /// <summary>Owner filter (<c>?owner=</c>).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Owner { get; set; }

    /// <summary>Sort column (<c>?sort=</c>).</summary>
    [BindProperty(SupportsGet = true)]
    public ScheduleSort Sort { get; set; }

    /// <summary>Reverse sort (<c>?desc=true</c>).</summary>
    [BindProperty(SupportsGet = true, Name = "desc")]
    public bool Descending { get; set; }

    /// <summary>The filtered, sorted rows.</summary>
    public IReadOnlyList<ScheduleListRow> Rows { get; private set; } = [];

    /// <summary>Summary over all active schedules.</summary>
    public ScheduleListSummary Summary { get; private set; } = new(0, 0, 0, 0, null, null);

    /// <summary>Number of deleted schedules (shown on the "Deleted" tab).</summary>
    public int DeletedCount { get; private set; }

    /// <summary>Users, for the owner filter.</summary>
    public IReadOnlyList<UserSummary> Users { get; private set; } = [];

    /// <summary>Global sync settings (pause).</summary>
    public SyncSettings Settings { get; private set; } = new();

    /// <summary>How the app is connected to Amazon.</summary>
    public IAmazonConnectionInfo Connection => connection;

    /// <summary>The latest connection test, if any.</summary>
    public ConnectionTestResult? LastTest => tester.LastResult;

    /// <summary>Current time, for relative times.</summary>
    public DateTimeOffset Now { get; } = clock.GetUtcNow();

    /// <summary>Whether any filter (other than the view tab) is applied.</summary>
    public bool IsFiltered =>
        !string.IsNullOrWhiteSpace(Query) || Status != ScheduleStatusFilter.All || ReportType is not null
        || LastRun != LastRunFilter.All || !string.IsNullOrEmpty(Owner);

    /// <summary>Loads the list.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var all = await schedules.ListIncludingDeletedAsync(cancellationToken);
        var latest = await runs.GetLatestByScheduleAsync(cancellationToken);

        Rows = ScheduleList.Apply(all, latest, new ScheduleListFilter(Query, View, Status, ReportType, LastRun, Owner, Sort, Descending));
        Summary = ScheduleList.Summarize(all, latest);
        DeletedCount = all.Count(s => s.IsDeleted);
        Users = await identity.ListUsersAsync(cancellationToken);
        Settings = await syncSettings.GetAsync(cancellationToken);
    }

    /// <summary>
    /// Builds a link to this page with the current filters, changing some of them. A <see langword="null"/>
    /// value removes that parameter (back to its default).
    /// </summary>
    /// <param name="changes">Query parameters to set or clear.</param>
    /// <returns>A relative URL.</returns>
    public string Link(params (string Key, string? Value)[] changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var values = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in changes)
        {
            if (string.IsNullOrEmpty(value))
            {
                values.Remove(key);
            }
            else
            {
                values[key] = value;
            }
        }

        var query = new QueryBuilder(values.Where(v => !string.IsNullOrEmpty(v.Value)));
        return Url.Page("Index") + query.ToQueryString();
    }

    /// <summary>Link for a sortable column header: toggles direction when it's already the sort.</summary>
    /// <param name="column">Column.</param>
    /// <returns>A relative URL.</returns>
    public string SortLink(ScheduleSort column) =>
        Link(("sort", column == ScheduleSort.Name ? null : column.ToString()), ("desc", Sort == column && !Descending ? "true" : null));

    /// <summary>Arrow for a sortable column header.</summary>
    /// <param name="column">Column.</param>
    /// <returns>"↑", "↓", or empty.</returns>
    public string SortArrow(ScheduleSort column) => Sort != column ? string.Empty : Descending ? "↓" : "↑";

    /// <summary>Enables or disables a schedule.</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="enabled">Desired state.</param>
    /// <param name="back">The list's query string, to return to the same filters.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostSetEnabledAsync(int id, bool enabled, string? back, CancellationToken cancellationToken)
    {
        var result = await scheduleService.SetEnabledAsync(id, enabled, User.Identity!.Name!, cancellationToken);
        Report(result, enabled ? "Schedule turned on." : "Schedule turned off. It can still be run manually.");
        return BackToList(back);
    }

    /// <summary>Queues an immediate run.</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="back">The list's query string.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the run history, or back to the list with an error.</returns>
    public async Task<IActionResult> OnPostRunNowAsync(int id, string? back, CancellationToken cancellationToken)
    {
        var result = await scheduleService.RunNowAsync(id, User.Identity!.Name!, cancellationToken);
        if (result.IsFailure)
        {
            TempData[StatusMessage.Error] = result.Error;
            return BackToList(back);
        }

        TempData[StatusMessage.Success] = "Run queued. This page refreshes on its own while it runs.";
        return RedirectToPage("Runs", new { scheduleId = id });
    }

    /// <summary>Queues a backfill for the last N days or a date range.</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="days">Quick pick (7/14/30); ignored when a range is given.</param>
    /// <param name="from">Range start date (UTC day), optional.</param>
    /// <param name="to">Range end date (UTC day, inclusive), optional.</param>
    /// <param name="back">The list's query string.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect to the run history, or back to the list with an error.</returns>
    public async Task<IActionResult> OnPostBackfillAsync(int id, int? days, DateOnly? from, DateOnly? to, string? back, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        BackfillWindow? window = (from, to, days) switch
        {
            ({ } start, { } end, _) => new BackfillWindow(
                new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                // Inclusive end day: through midnight after it, capped to now by the service.
                new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)),
            (null, null, { } n) => BackfillWindow.LastDays(n, now),
            _ => null,
        };

        if (window is null)
        {
            TempData[StatusMessage.Error] = "Choose how many days to backfill, or both a start and an end date.";
            return BackToList(back);
        }

        // An end date of today would reach past now; the service rejects future ends, so clamp here.
        if (window.End > now)
        {
            window = window with { End = now };
        }

        var result = await scheduleService.BackfillAsync(id, window, User.Identity!.Name!, cancellationToken);
        if (result.IsFailure)
        {
            TempData[StatusMessage.Error] = result.Error;
            return BackToList(back);
        }

        TempData[StatusMessage.Success] = $"Backfill {window} queued. Amazon can take several minutes to build a large report.";
        return RedirectToPage("Runs", new { scheduleId = id });
    }

    /// <summary>Soft-deletes a schedule.</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="back">The list's query string.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostDeleteAsync(int id, string? back, CancellationToken cancellationToken)
    {
        var result = await scheduleService.DeleteAsync(id, User.Identity!.Name!, cancellationToken);
        Report(result, "Schedule deleted. Its run history is kept, and it can be restored from the Deleted tab.");
        return BackToList(back);
    }

    /// <summary>Restores a deleted schedule (turned off).</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="back">The list's query string.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostRestoreAsync(int id, string? back, CancellationToken cancellationToken)
    {
        var result = await scheduleService.RestoreAsync(id, User.Identity!.Name!, cancellationToken);
        Report(result, "Schedule restored. It's turned off; switch it on when you're ready.");
        return BackToList(back);
    }

    /// <summary>Pauses or resumes all scheduled runs.</summary>
    /// <param name="paused">Desired state.</param>
    /// <param name="back">The list's query string.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostPauseAsync(bool paused, string? back, CancellationToken cancellationToken)
    {
        var result = await scheduleService.SetPausedAsync(paused, User.Identity!.Name!, cancellationToken);
        Report(result, paused
            ? "All scheduled syncs paused. Run now and backfills still work."
            : "Scheduled syncs resumed. Overdue schedules run once now, then continue as usual.");
        return BackToList(back);
    }

    /// <summary>Applies one action to the schedules ticked in the list.</summary>
    /// <param name="action">What to do.</param>
    /// <param name="ids">Ticked schedule ids.</param>
    /// <param name="back">The list's query string.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostBulkAsync(BulkScheduleAction action, int[] ids, string? back, CancellationToken cancellationToken)
    {
        var result = await scheduleService.BulkAsync(action, ids ?? [], User.Identity!.Name!, cancellationToken);
        if (result.IsFailure)
        {
            TempData[StatusMessage.Error] = result.Error;
            return BackToList(back);
        }

        var (succeeded, errors) = (result.Value.Succeeded, result.Value.Errors);
        var noun = succeeded == 1 ? "schedule" : "schedules";
        var done = action switch
        {
            BulkScheduleAction.Disable => $"{succeeded} {noun} turned off. They can still be run manually.",
            BulkScheduleAction.Enable => $"{succeeded} {noun} turned on.",
            BulkScheduleAction.Delete => $"{succeeded} {noun} deleted. Run history is kept; restore them from the Deleted tab.",
            _ => $"{succeeded} {noun} restored (turned off).",
        };

        if (succeeded > 0)
        {
            TempData[StatusMessage.Success] = done;
        }

        if (errors.Count > 0)
        {
            TempData[StatusMessage.Error] = $"{errors.Count} skipped: " + string.Join(" ", errors.Distinct());
        }

        return BackToList(back);
    }

    /// <summary>Tests the Amazon connection; the result shows next to the status light.</summary>
    /// <param name="back">The list's query string.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A redirect back to the list.</returns>
    public async Task<IActionResult> OnPostTestConnectionAsync(string? back, CancellationToken cancellationToken)
    {
        await tester.TestAsync(cancellationToken);
        return BackToList(back);
    }

    private void Report(Application.Common.Result result, string success) =>
        TempData[result.IsSuccess ? StatusMessage.Success : StatusMessage.Error] = result.IsSuccess ? success : result.Error;

    /// <summary>Redirects to the list with the filters the user was looking at.</summary>
    private RedirectResult BackToList(string? back)
    {
        // Only a query string is accepted, so this can never redirect off-site.
        var query = back is { Length: > 1 } && back[0] == '?' ? back : string.Empty;
        return Redirect(Url.Page("Index") + query);
    }
}
