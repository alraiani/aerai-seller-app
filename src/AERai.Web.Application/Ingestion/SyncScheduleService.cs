using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Ingestion;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Default <see cref="ISyncScheduleService"/>.
/// </summary>
/// <param name="repository">Schedule persistence.</param>
/// <param name="runs">Run history, to spot a range that was already backfilled.</param>
/// <param name="settings">App-wide sync settings (pause).</param>
/// <param name="queue">Scheduler inbox: manual runs, and a wake after every change to when schedules run.</param>
/// <param name="connection">Amazon connection state.</param>
/// <param name="identity">User lookups for the schedule owner.</param>
/// <param name="marketplaces">Marketplace lookups for the schedule's marketplace.</param>
/// <param name="clock">Clock for next-run calculation.</param>
/// <param name="logger">Logger.</param>
public sealed partial class SyncScheduleService(
    ISyncScheduleRepository repository,
    ISyncRunRepository runs,
    ISyncSettingsRepository settings,
    IManualRunChannel queue,
    IAmazonConnectionInfo connection,
    IIdentityService identity,
    IMarketplaceQueries marketplaces,
    TimeProvider clock,
    ILogger<SyncScheduleService> logger) : ISyncScheduleService
{
    /// <summary>
    /// Shortest allowed interval. SP-API's createReport quota is about one request per minute per
    /// seller, shared by all schedules, so very frequent pulls would just queue behind each other.
    /// </summary>
    public const int MinIntervalMinutes = 15;

    /// <summary>Longest allowed interval (one week).</summary>
    public const int MaxIntervalMinutes = 7 * 24 * 60;

    /// <summary>Amazon caps the orders report data window at 30 days.</summary>
    public const int MaxLookbackDays = 30;

    /// <summary>How much earlier than the requested end an earlier backfill may have ended and still count as covering it.</summary>
    public static readonly TimeSpan BackfillCoverageEndTolerance = TimeSpan.FromDays(1);

    /// <summary>
    /// Most runs "backfill all" queues in one go. The manual-run inbox holds 100 and waits when full, so this
    /// keeps a click from hanging the request, and it stops a typo'd range from queueing years of work.
    /// </summary>
    public const int MaxBackfillAllRuns = 90;

    /// <summary>Longest notes text.</summary>
    public const int MaxNotesLength = 500;

    /// <inheritdoc/>
    public async Task<Result<int>> CreateAsync(SyncScheduleInput input, string user, CancellationToken cancellationToken)
    {
        var validated = Validate(input);
        if (validated.IsFailure)
        {
            return Result.Failure<int>(validated.Error);
        }

        var conflict = await CheckNameAndOwnerAsync(validated.Value, excludeId: null, cancellationToken).ConfigureAwait(false);
        if (conflict.IsFailure)
        {
            return Result.Failure<int>(conflict.Error);
        }

        var schedule = validated.Value;
        var now = clock.GetUtcNow();
        schedule.CreatedAt = now;
        schedule.UpdatedAt = now;
        schedule.UpdatedBy = user;
        schedule.NextRunAt = schedule.IsEnabled ? ScheduleCalculator.NextRunAfter(schedule, now) : null;

        var id = await repository.AddAsync(schedule, cancellationToken).ConfigureAwait(false);
        queue.Wake();
        LogChanged(id, user);
        return Result.Success(id);
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateAsync(int id, SyncScheduleInput input, string user, CancellationToken cancellationToken)
    {
        var existing = await repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("Schedule not found.");
        }

        var validated = Validate(input);
        if (validated.IsFailure)
        {
            return Result.Failure(validated.Error);
        }

        var conflict = await CheckNameAndOwnerAsync(validated.Value, id, cancellationToken).ConfigureAwait(false);
        if (conflict.IsFailure)
        {
            return conflict;
        }

        var updated = validated.Value;
        updated.Id = id;
        updated.CreatedAt = existing.CreatedAt;
        updated.LastRunAt = existing.LastRunAt;
        updated.LastSuccessfulDataEnd = existing.ReportType == updated.ReportType ? existing.LastSuccessfulDataEnd : null;
        updated.UpdatedAt = clock.GetUtcNow();
        updated.UpdatedBy = user;
        updated.NextRunAt = updated.IsEnabled ? ScheduleCalculator.NextRunAfter(updated, updated.UpdatedAt) : null;

        await repository.UpdateSettingsAsync(updated, cancellationToken).ConfigureAwait(false);
        queue.Wake();
        LogChanged(id, user);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> SetEnabledAsync(int id, bool enabled, string user, CancellationToken cancellationToken)
    {
        var schedule = await repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return Result.Failure("Schedule not found.");
        }

        schedule.IsEnabled = enabled;
        schedule.UpdatedAt = clock.GetUtcNow();
        schedule.UpdatedBy = user;
        schedule.NextRunAt = enabled ? ScheduleCalculator.NextRunAfter(schedule, schedule.UpdatedAt) : null;

        await repository.UpdateSettingsAsync(schedule, cancellationToken).ConfigureAwait(false);
        queue.Wake();
        LogChanged(id, user);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<RunNowResult>> RunNowAsync(int id, string user, CancellationToken cancellationToken)
    {
        if (!connection.CanRun)
        {
            return Result.Failure<RunNowResult>($"Amazon is not connected: {connection.Problem}");
        }

        if (await repository.GetAsync(id, cancellationToken).ConfigureAwait(false) is not { } schedule)
        {
            return Result.Failure<RunNowResult>("Schedule not found.");
        }

        var wait = await RequestWaitAsync(schedule, cancellationToken).ConfigureAwait(false);
        await queue.EnqueueAsync(new ManualRunRequest(id, user), cancellationToken).ConfigureAwait(false);
        return Result.Success(new RunNowResult(wait));
    }

    /// <inheritdoc/>
    public async Task<Result> BackfillAsync(int id, BackfillWindow window, string user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);

        var now = clock.GetUtcNow();
        if (window.End <= window.Start)
        {
            return Result.Failure("The backfill start must be before its end.");
        }

        // A minute of slack so a window ending "now" in the browser isn't rejected by clock skew.
        if (window.End > now.AddMinutes(1))
        {
            return Result.Failure("The backfill can't end in the future.");
        }

        if (!connection.CanRun)
        {
            return Result.Failure($"Amazon is not connected: {connection.Problem}");
        }

        var schedule = await repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return Result.Failure("Schedule not found.");
        }

        // Settlement reports are listed, and Amazon lists only the last 90 days. A range that starts earlier is
        // shortened by the run itself; one that ends before the limit has nothing to list at all.
        var oldestListable = now - ReportIngestionService.MaxListingAge;
        if (schedule.ReportType == AmazonReportType.Settlements && window.End <= oldestListable)
        {
            return Result.Failure(
                $"Amazon only lists settlement reports from the last 90 days (since {oldestListable:yyyy-MM-dd}). " +
                "Download older settlements from Seller Central, upload them to raw storage, and use Tools → Refresh from blobs.");
        }

        // Amazon Ads keeps report data for 95 days; the run itself starts a partly older range at the limit.
        var oldestAdsDay = now - ReportIngestionService.MaxAdsReportAge;
        if (schedule.ReportType == AmazonReportType.AdsSpend && window.End <= oldestAdsDay)
        {
            return Result.Failure($"Amazon Ads only keeps advertising data for 95 days (since {oldestAdsDay:yyyy-MM-dd}), so older spend can't be pulled.");
        }

        // The 30 days is Amazon's limit for one report. The Orders API pages through results instead of
        // building a report, so it can take any range in one run.
        if (schedule.ReportType != AmazonReportType.OrdersApi && window.Span > TimeSpan.FromDays(MaxLookbackDays))
        {
            return Result.Failure($"Backfill at most {MaxLookbackDays} days at a time (Amazon's limit for one report).");
        }

        if (schedule.ReportType.IsSnapshot())
        {
            return Result.Failure("Inventory reports are a current snapshot, so there is no history to backfill.");
        }

        var clamped = window.End > now ? window with { End = now } : window;
        await queue.EnqueueAsync(new ManualRunRequest(id, user, clamped), cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<SyncAllResult>> SyncAllAsync(string user, CancellationToken cancellationToken)
    {
        if (!connection.CanRun)
        {
            return Result.Failure<SyncAllResult>($"Amazon is not connected: {connection.Problem}");
        }

        var schedules = await EnabledInActiveMarketplacesAsync(cancellationToken).ConfigureAwait(false);
        if (schedules.Count == 0)
        {
            return Result.Failure<SyncAllResult>("Turn on at least one schedule first.");
        }

        var queued = new List<string>();
        var skipped = new List<string>();
        var waiting = new List<RequestWait>();
        foreach (var schedule in schedules)
        {
            // A run still waiting on Amazon for this schedule is already doing this work.
            if (await runs.HasPendingAsync(schedule.Id, cancellationToken).ConfigureAwait(false))
            {
                skipped.Add(schedule.Name);
                continue;
            }

            if (await RequestWaitAsync(schedule, cancellationToken).ConfigureAwait(false) is { } wait)
            {
                waiting.Add(wait);
            }

            await queue.EnqueueAsync(new ManualRunRequest(schedule.Id, user), cancellationToken).ConfigureAwait(false);
            queued.Add(schedule.Name);
        }

        LogSyncAll(queued.Count, skipped.Count, user);
        return Result.Success(new SyncAllResult(queued, skipped, waiting));
    }

    /// <summary>
    /// Whether a run queued now would have to wait for the report's request limit, judged from the last
    /// request for the same report type and marketplace. Only reports with a cooldown are looked up.
    /// </summary>
    private async Task<RequestWait?> RequestWaitAsync(SyncSchedule schedule, CancellationToken cancellationToken)
    {
        if (schedule.ReportType.RequestCooldown() <= TimeSpan.Zero)
        {
            return null;
        }

        var lastRequest = await runs.GetLastReportRequestAsync(schedule.ReportType, schedule.MarketplaceId, cancellationToken).ConfigureAwait(false);
        return ReportRequestCooldown.WaitUntil(schedule.ReportType, lastRequest, clock.GetUtcNow()) is { } until
            ? new RequestWait(schedule.Name, until, ReportRequestCooldown.Describe(schedule.ReportType, until))
            : null;
    }

    /// <summary>Switched-on schedules in active marketplaces, by marketplace then report type.</summary>
    private async Task<IReadOnlyList<SyncSchedule>> EnabledInActiveMarketplacesAsync(CancellationToken cancellationToken)
    {
        var active = (await marketplaces.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .Where(m => m.IsActive)
            .Select(m => m.MarketplaceId)
            .ToHashSet(StringComparer.Ordinal);
        var schedules = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
        return [.. schedules
            .Where(s => s.IsEnabled && active.Contains(s.MarketplaceId))
            .OrderBy(s => s.MarketplaceId, StringComparer.Ordinal)
            .ThenBy(s => s.ReportType)];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SyncSchedule>> GetBackfillAllTargetsAsync(CancellationToken cancellationToken)
    {
        var enabled = await EnabledInActiveMarketplacesAsync(cancellationToken).ConfigureAwait(false);
        return [.. enabled.Where(s => !s.ReportType.IsSnapshot())];
    }

    /// <inheritdoc/>
    public async Task<Result<BackfillAllResult>> BackfillAllAsync(BackfillWindow window, string user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);

        var now = clock.GetUtcNow();
        if (window.End <= window.Start)
        {
            return Result.Failure<BackfillAllResult>("The backfill start must be before its end.");
        }

        if (window.End > now.AddMinutes(1))
        {
            return Result.Failure<BackfillAllResult>("The backfill can't end in the future.");
        }

        if (!connection.CanRun)
        {
            return Result.Failure<BackfillAllResult>($"Amazon is not connected: {connection.Problem}");
        }

        var targets = await GetBackfillAllTargetsAsync(cancellationToken).ConfigureAwait(false);
        if (targets.Count == 0)
        {
            return Result.Failure<BackfillAllResult>("Turn on at least one schedule that has history to backfill (orders or settlements) first.");
        }

        var clamped = window.End > now ? window with { End = now } : window;

        // The Orders API takes any range in one run, so it goes last: the scheduler does one thing at a
        // time and that run is the slow one, which would otherwise hold up every quick run behind it.
        var plan = targets
            .OrderBy(s => s.ReportType == AmazonReportType.OrdersApi ? 1 : 0)
            .Select(s => PlanFor(s, clamped, now))
            .ToList();

        var total = plan.Sum(p => p.Windows.Count);
        if (total == 0)
        {
            return Result.Failure<BackfillAllResult>(plan.Select(p => p.Note).FirstOrDefault(n => n is not null) ?? "There is nothing to backfill in that range.");
        }

        if (total > MaxBackfillAllRuns)
        {
            return Result.Failure<BackfillAllResult>(
                $"That range needs {total} runs across {plan.Count} schedules; at most {MaxBackfillAllRuns} can be queued at once. Choose a shorter range.");
        }

        var results = new List<BackfillAllScheduleResult>();
        foreach (var (schedule, windows, note) in plan)
        {
            // Checked before queueing so a run can't match itself. A repeat is only reported, never refused.
            var covered = 0;
            foreach (var w in windows)
            {
                var earlier = await runs.FindCoveringBackfillAsync(
                    schedule.ReportType, schedule.MarketplaceId, w.Start, w.End - BackfillCoverageEndTolerance, cancellationToken).ConfigureAwait(false);
                covered += earlier is null ? 0 : 1;
            }

            foreach (var w in windows)
            {
                await queue.EnqueueAsync(new ManualRunRequest(schedule.Id, user, w), cancellationToken).ConfigureAwait(false);
            }

            results.Add(new BackfillAllScheduleResult(schedule.Name, schedule.ReportType, windows.Count, covered, note));
        }

        LogBackfillAll(plan.Count, total, clamped, user);
        return Result.Success(new BackfillAllResult(clamped, results));
    }

    /// <summary>
    /// The windows one schedule needs for a "backfill all" range. The Orders API takes the whole range in one
    /// run; report-based schedules need 30-day windows. Settlements can only be listed for the last 90 days and
    /// Amazon Ads keeps 95 days of data, so anything older is dropped and the result says so.
    /// </summary>
    private static (SyncSchedule Schedule, IReadOnlyList<BackfillWindow> Windows, string? Note) PlanFor(
        SyncSchedule schedule, BackfillWindow range, DateTimeOffset now)
    {
        if (schedule.ReportType == AmazonReportType.OrdersApi)
        {
            return (schedule, [range], null);
        }

        if (HistoryLimit(schedule.ReportType, now) is not { } limit || range.Start >= limit.Oldest)
        {
            return (schedule, BackfillSplitter.Split(range, MaxLookbackDays), null);
        }

        if (range.End <= limit.Oldest)
        {
            return (schedule, [], $"Nothing queued: the whole range is older than {limit.Days} days. {limit.Advice}");
        }

        var available = range with { Start = limit.Oldest };
        return (schedule, BackfillSplitter.Split(available, MaxLookbackDays), $"Starts at {limit.Oldest:yyyy-MM-dd} instead of {range.Start:yyyy-MM-dd}. {limit.Advice}");
    }

    /// <summary>How far back Amazon can still provide a report type, when it has a limit.</summary>
    private static (DateTimeOffset Oldest, int Days, string Advice)? HistoryLimit(AmazonReportType reportType, DateTimeOffset now) => reportType switch
    {
        AmazonReportType.Settlements => (now - ReportIngestionService.MaxListingAge, 90,
            "Amazon only lists settlement reports from the last 90 days; download older ones from Seller Central, upload them to raw storage, and use Refresh from blobs."),
        AmazonReportType.AdsSpend => (now - ReportIngestionService.MaxAdsReportAge, 95,
            "Amazon Ads only keeps advertising data for 95 days, so older spend can't be pulled."),
        _ => null,
    };

    /// <inheritdoc/>
    public async Task<BackfillCoverage?> FindCoveringBackfillAsync(int id, BackfillWindow window, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);

        var schedule = await repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (schedule is null || schedule.ReportType.IsSnapshot())
        {
            return null;
        }

        // A range ending "now" is asked for again a little later with a later "now", so the end gets a
        // day of slack; the start must still be covered exactly.
        var covering = await runs.FindCoveringBackfillAsync(
            schedule.ReportType, schedule.MarketplaceId, window.Start, window.End - BackfillCoverageEndTolerance, cancellationToken).ConfigureAwait(false);
        return covering is { BackfillStart: { } start, BackfillEnd: { } end, CompletedAt: { } completedAt }
            ? new BackfillCoverage(new BackfillWindow(start, end), completedAt, covering.TriggeredBy)
            : null;
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteAsync(int id, string user, CancellationToken cancellationToken)
    {
        if (!await repository.SoftDeleteAsync(id, user, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure("Schedule not found.");
        }

        queue.Wake();
        LogDeleted(id, user);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> RestoreAsync(int id, string user, CancellationToken cancellationToken)
    {
        var schedule = await repository.GetIncludingDeletedAsync(id, cancellationToken).ConfigureAwait(false);
        if (schedule is not { IsDeleted: true })
        {
            return Result.Failure("Deleted schedule not found.");
        }

        if (await repository.NameExistsAsync(schedule.Name, id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure($"Another schedule is already named '{schedule.Name}'. Rename it first, then restore this one.");
        }

        await repository.RestoreAsync(id, user, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        queue.Wake();
        LogRestored(id, user);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> SetPausedAsync(bool paused, string user, CancellationToken cancellationToken)
    {
        await settings.SetPausedAsync(paused, user, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        queue.Wake();
        LogPaused(paused, user);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<BulkScheduleResult>> BulkAsync(BulkScheduleAction action, IReadOnlyCollection<int> ids, string user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return Result.Failure<BulkScheduleResult>("Select at least one schedule first.");
        }

        var succeeded = 0;
        var errors = new List<string>();
        foreach (var id in ids.Distinct())
        {
            var result = action switch
            {
                BulkScheduleAction.Disable => await SetEnabledAsync(id, enabled: false, user, cancellationToken).ConfigureAwait(false),
                BulkScheduleAction.Enable => await SetEnabledAsync(id, enabled: true, user, cancellationToken).ConfigureAwait(false),
                BulkScheduleAction.Delete => await DeleteAsync(id, user, cancellationToken).ConfigureAwait(false),
                BulkScheduleAction.Restore => await RestoreAsync(id, user, cancellationToken).ConfigureAwait(false),
                _ => Result.Failure($"Unknown action '{action}'."),
            };

            if (result.IsSuccess)
            {
                succeeded++;
            }
            else
            {
                errors.Add(result.Error);
            }
        }

        return Result.Success(new BulkScheduleResult(succeeded, errors));
    }

    /// <inheritdoc/>
    public Result<SyncSchedule> Validate(SyncScheduleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 100)
        {
            return Result.Failure<SyncSchedule>("Name is required (up to 100 characters).");
        }

        if (!Enum.IsDefined(input.ReportType))
        {
            return Result.Failure<SyncSchedule>("Choose a report type.");
        }

        // Whether the marketplace exists and is active needs the database; that check is in CheckNameAndOwnerAsync.
        if (string.IsNullOrWhiteSpace(input.MarketplaceId))
        {
            return Result.Failure<SyncSchedule>("Choose a marketplace.");
        }

        if (input.Frequency == ScheduleFrequency.Interval
            && input.IntervalMinutes is not (>= MinIntervalMinutes and <= MaxIntervalMinutes))
        {
            return Result.Failure<SyncSchedule>(
                $"Interval must be between {MinIntervalMinutes} minutes and {MaxIntervalMinutes / (24 * 60)} days.");
        }

        if (input.Frequency == ScheduleFrequency.Interval && (input.ActiveFrom is null) != (input.ActiveUntil is null))
        {
            return Result.Failure<SyncSchedule>("Set both the start and end of the active hours, or leave both empty to run around the clock.");
        }

        if (input.Frequency == ScheduleFrequency.Interval && input.ActiveFrom is { } from && from == input.ActiveUntil)
        {
            return Result.Failure<SyncSchedule>("Active hours must start and end at different times. Leave both empty to run around the clock.");
        }

        if (input.Frequency == ScheduleFrequency.Daily && input.DailyTime is null)
        {
            return Result.Failure<SyncSchedule>("Choose a time of day for a daily schedule.");
        }

        if (!Enum.IsDefined(input.Frequency))
        {
            return Result.Failure<SyncSchedule>("Choose how often the schedule runs.");
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(input.TimeZoneId, out _))
        {
            return Result.Failure<SyncSchedule>($"Unknown time zone '{input.TimeZoneId}'.");
        }

        if (input.LookbackDays is < 1 or > MaxLookbackDays)
        {
            return Result.Failure<SyncSchedule>($"Lookback must be between 1 and {MaxLookbackDays} days.");
        }

        if (input.Notes?.Trim().Length > MaxNotesLength)
        {
            return Result.Failure<SyncSchedule>($"Notes can be up to {MaxNotesLength} characters.");
        }

        return Result.Success(new SyncSchedule
        {
            Name = input.Name.Trim(),
            ReportType = input.ReportType,
            MarketplaceId = input.MarketplaceId,
            IsEnabled = input.IsEnabled,
            Frequency = input.Frequency,

            // Keep only the field that applies, so the stored schedule is unambiguous.
            IntervalMinutes = input.Frequency == ScheduleFrequency.Interval ? input.IntervalMinutes : null,
            DailyTime = input.Frequency == ScheduleFrequency.Daily ? input.DailyTime : null,
            ActiveFrom = input.Frequency == ScheduleFrequency.Interval ? input.ActiveFrom : null,
            ActiveUntil = input.Frequency == ScheduleFrequency.Interval ? input.ActiveUntil : null,
            TimeZoneId = input.TimeZoneId,
            LookbackDays = input.LookbackDays,
            AutoPromote = input.AutoPromote,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            OwnerEmail = string.IsNullOrWhiteSpace(input.OwnerEmail) ? null : input.OwnerEmail.Trim(),
            UpdatedBy = string.Empty,
        });
    }

    /// <summary>Checks rules that need the database: unique name, an active marketplace, and an owner who is a real user.</summary>
    private async Task<Result> CheckNameAndOwnerAsync(SyncSchedule schedule, int? excludeId, CancellationToken cancellationToken)
    {
        if (await repository.NameExistsAsync(schedule.Name, excludeId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure($"Another schedule is already named '{schedule.Name}'.");
        }

        var all = await marketplaces.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (!all.Any(m => m.IsActive && m.MarketplaceId == schedule.MarketplaceId))
        {
            return Result.Failure("Choose an active marketplace.");
        }

        if (schedule.OwnerEmail is { } owner)
        {
            var users = await identity.ListUsersAsync(cancellationToken).ConfigureAwait(false);
            if (!users.Any(u => string.Equals(u.Email, owner, StringComparison.OrdinalIgnoreCase)))
            {
                return Result.Failure($"Owner '{owner}' is not a user of this app.");
            }
        }

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync schedule {ScheduleId} changed by {User}")]
    private partial void LogChanged(int scheduleId, string user);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync schedule {ScheduleId} deleted by {User}")]
    private partial void LogDeleted(int scheduleId, string user);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync schedule {ScheduleId} restored by {User}")]
    private partial void LogRestored(int scheduleId, string user);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduled syncs paused = {Paused} by {User}")]
    private partial void LogPaused(bool paused, string user);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backfill all queued {Runs} runs for {Schedules} schedules over {Window} by {User}")]
    private partial void LogBackfillAll(int schedules, int runs, BackfillWindow window, string user);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync all queued {Queued} runs and skipped {Skipped} by {User}")]
    private partial void LogSyncAll(int queued, int skipped, string user);
}
