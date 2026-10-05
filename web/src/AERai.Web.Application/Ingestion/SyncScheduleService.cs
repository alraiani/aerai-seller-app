using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Ingestion;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Default <see cref="ISyncScheduleService"/>.
/// </summary>
/// <param name="repository">Schedule persistence.</param>
/// <param name="queue">Manual-run queue.</param>
/// <param name="connection">Amazon connection state.</param>
/// <param name="clock">Clock for next-run calculation.</param>
/// <param name="logger">Logger.</param>
public sealed partial class SyncScheduleService(
    ISyncScheduleRepository repository,
    IManualRunChannel queue,
    IAmazonConnectionInfo connection,
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

    /// <inheritdoc/>
    public async Task<Result<int>> CreateAsync(SyncScheduleInput input, string user, CancellationToken cancellationToken)
    {
        var validated = Validate(input);
        if (validated.IsFailure)
        {
            return Result.Failure<int>(validated.Error);
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
    public async Task<Result> BackfillAsync(int id, int days, string user, CancellationToken cancellationToken)
    {
        if (days is < 1 or > MaxLookbackDays)
        {
            return Result.Failure($"Backfill must be between 1 and {MaxLookbackDays} days.");
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

        await queue.EnqueueAsync(new ManualRunRequest(id, user, days), cancellationToken).ConfigureAwait(false);
        return Result.Success();
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
            UpdatedBy = string.Empty,
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync schedule {ScheduleId} changed by {User}")]
    private partial void LogChanged(int scheduleId, string user);
}
