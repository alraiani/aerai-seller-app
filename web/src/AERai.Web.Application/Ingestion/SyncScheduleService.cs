using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Ingestion;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Default <see cref="ISyncScheduleService"/>.
/// </summary>
/// <param name="repository">Schedule persistence.</param>
/// <param name="settings">App-wide sync settings (pause).</param>
/// <param name="queue">Manual-run queue.</param>
/// <param name="connection">Amazon connection state.</param>
/// <param name="identity">User lookups for the schedule owner.</param>
/// <param name="clock">Clock for next-run calculation.</param>
/// <param name="logger">Logger.</param>
public sealed partial class SyncScheduleService(
    ISyncScheduleRepository repository,
    ISyncSettingsRepository settings,
    IManualRunChannel queue,
    IAmazonConnectionInfo connection,
    IIdentityService identity,
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
        LogChanged(id, user);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> RunNowAsync(int id, string user, CancellationToken cancellationToken)
    {
        if (!connection.CanRun)
        {
            return Result.Failure($"Amazon is not connected: {connection.Problem}");
        }

        if (await repository.GetAsync(id, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure("Schedule not found.");
        }

        await queue.EnqueueAsync(new ManualRunRequest(id, user), cancellationToken).ConfigureAwait(false);
        return Result.Success();
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

        if (window.Span > TimeSpan.FromDays(MaxLookbackDays))
        {
            return Result.Failure($"Backfill at most {MaxLookbackDays} days at a time (Amazon's limit for one report).");
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

        if (schedule.ReportType == AmazonReportType.FbaInventory)
        {
            return Result.Failure("FBA inventory is a current snapshot, so there is no history to backfill.");
        }

        var clamped = window.End > now ? window with { End = now } : window;
        await queue.EnqueueAsync(new ManualRunRequest(id, user, clamped), cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteAsync(int id, string user, CancellationToken cancellationToken)
    {
        if (!await repository.SoftDeleteAsync(id, user, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure("Schedule not found.");
        }

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
        LogRestored(id, user);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> SetPausedAsync(bool paused, string user, CancellationToken cancellationToken)
    {
        await settings.SetPausedAsync(paused, user, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
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

        if (input.Frequency == ScheduleFrequency.Interval
            && input.IntervalMinutes is not (>= MinIntervalMinutes and <= MaxIntervalMinutes))
        {
            return Result.Failure<SyncSchedule>(
                $"Interval must be between {MinIntervalMinutes} minutes and {MaxIntervalMinutes / (24 * 60)} days.");
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
            IsEnabled = input.IsEnabled,
            Frequency = input.Frequency,

            // Keep only the field that applies, so the stored schedule is unambiguous.
            IntervalMinutes = input.Frequency == ScheduleFrequency.Interval ? input.IntervalMinutes : null,
            DailyTime = input.Frequency == ScheduleFrequency.Daily ? input.DailyTime : null,
            TimeZoneId = input.TimeZoneId,
            LookbackDays = input.LookbackDays,
            AutoPromote = input.AutoPromote,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            OwnerEmail = string.IsNullOrWhiteSpace(input.OwnerEmail) ? null : input.OwnerEmail.Trim(),
            UpdatedBy = string.Empty,
        });
    }

    /// <summary>Checks rules that need the database: unique name and an owner who is a real user.</summary>
    private async Task<Result> CheckNameAndOwnerAsync(SyncSchedule schedule, int? excludeId, CancellationToken cancellationToken)
    {
        if (await repository.NameExistsAsync(schedule.Name, excludeId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure($"Another schedule is already named '{schedule.Name}'.");
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
}
