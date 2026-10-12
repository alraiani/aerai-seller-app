using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.Ingestion;

/// <summary>
/// Background service that runs due schedules and "Run now" requests one at a time, and checks on
/// reports Amazon is still generating for earlier runs.
/// </summary>
/// <remarks>
/// <para>Work is sequential on purpose: SP-API quotas are per seller, so parallel pulls would only
/// queue behind each other in the rate limiter. Waiting for Amazon to generate a report is not
/// work: the pending report is saved on the run and checked on later, so one slow report never
/// holds up other schedules or a "Run now".</para>
/// <para>Between events the worker sleeps rather than polling the database on a fixed tick, so a
/// serverless database can auto-pause when nothing is due.</para>
/// <para>Each due slot is claimed atomically in the database before running, so several app
/// instances never run the same slot twice. After downtime, an overdue schedule runs once and then
/// resumes its normal cadence — missed slots are not replayed (Orders windows continue from the
/// last successful run, so no data is skipped).</para>
/// </remarks>
/// <param name="scopes">Creates a DI scope per run (services are scoped to the DbContext).</param>
/// <param name="channel">Manual-run requests and wake signals.</param>
/// <param name="connection">Whether Amazon is reachable.</param>
/// <param name="options">Scheduler settings.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
internal sealed partial class SyncSchedulerWorker(
    IServiceScopeFactory scopes,
    IManualRunChannel channel,
    IAmazonConnectionInfo connection,
    IOptions<IngestionOptions> options,
    TimeProvider clock,
    ILogger<SyncSchedulerWorker> logger) : BackgroundService
{
    /// <summary>Recorded as <see cref="SyncRun.TriggeredBy"/> for scheduled runs.</summary>
    public const string SchedulerUser = "scheduler";

    /// <summary>Pause before retrying after an unexpected loop error (e.g. the database briefly unavailable).</summary>
    private static readonly TimeSpan ErrorRetryDelay = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long a claimed report check is reserved for this instance. If the instance dies mid-check,
    /// another instance (or this one after a restart) retries after this.
    /// </summary>
    private static readonly TimeSpan CheckLease = TimeSpan.FromMinutes(15);

    /// <summary>
    /// A run still marked running, not waiting on Amazon, and older than this was cut off by a crash:
    /// requesting a report or listing settlements takes seconds, not an hour.
    /// </summary>
    private static readonly TimeSpan AbandonedAfter = TimeSpan.FromHours(1);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.SchedulerEnabled)
        {
            LogDisabled();
            return;
        }

        LogStarted(connection.Mode, settings.SchedulerMaxSleep.TotalMinutes);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueWorkAsync(stoppingToken).ConfigureAwait(false);

                // Sleep until the next known event rather than polling the database on a fixed tick, so
                // the serverless database can auto-pause while nothing is due. A "Run now" or a schedule
                // change wakes the wait early.
                var sleep = await GetSleepAsync(settings, stoppingToken).ConfigureAwait(false);
                LogSleeping(sleep.TotalMinutes);
                if (await channel.DequeueAsync(sleep, stoppingToken).ConfigureAwait(false) is { } manual)
                {
                    await RunAsync(manual.ScheduleId, SyncTrigger.Manual, manual.RequestedBy, manual.Backfill, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // The scheduler loop must survive transient failures (e.g. the database being briefly unavailable).
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogLoopError(ex);
                await Task.Delay(ErrorRetryDelay, clock, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// How long to sleep: until the next due schedule or report check, capped at
    /// <see cref="IngestionOptions.SchedulerMaxSleep"/>.
    /// </summary>
    private async Task<TimeSpan> GetSleepAsync(IngestionOptions settings, CancellationToken cancellationToken)
    {
        if (!connection.CanRun)
        {
            return settings.SchedulerMaxSleep;
        }

        DateTimeOffset? nextRunAt = null;
        DateTimeOffset? nextCheckAt;
        await using (var scope = scopes.CreateAsyncScope())
        {
            // While paused, scheduled slots don't run, so they don't need a wake; un-pausing wakes the scheduler.
            if (!(await scope.ServiceProvider.GetRequiredService<ISyncSettingsRepository>().GetAsync(cancellationToken).ConfigureAwait(false)).IsPaused)
            {
                nextRunAt = await scope.ServiceProvider.GetRequiredService<ISyncScheduleRepository>().GetNextRunAtAsync(cancellationToken).ConfigureAwait(false);
            }

            // Runs already waiting on Amazon keep being checked while paused, like "Run now".
            nextCheckAt = await scope.ServiceProvider.GetRequiredService<ISyncRunRepository>().GetNextPendingCheckAtAsync(cancellationToken).ConfigureAwait(false);
        }

        return SchedulerSleep.Until(clock.GetUtcNow(), settings.SchedulerMaxSleep, nextRunAt, nextCheckAt);
    }

    /// <summary>Clears abandoned runs, checks on reports Amazon is generating, then starts due schedules.</summary>
    private async Task RunDueWorkAsync(CancellationToken cancellationToken)
    {
        if (!connection.CanRun)
        {
            return;
        }

        await FailAbandonedRunsAsync(cancellationToken).ConfigureAwait(false);
        await ContinuePendingRunsAsync(cancellationToken).ConfigureAwait(false);
        await RunDueSchedulesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task FailAbandonedRunsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var now = clock.GetUtcNow();
        var failed = await scope.ServiceProvider.GetRequiredService<ISyncRunRepository>()
            .FailAbandonedAsync(now - AbandonedAfter, "Interrupted: the app stopped before this run finished. Run it again if needed.", now, cancellationToken)
            .ConfigureAwait(false);
        if (failed > 0)
        {
            LogAbandoned(failed);
        }
    }

    private async Task ContinuePendingRunsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SyncRun> due;
        await using (var scope = scopes.CreateAsyncScope())
        {
            due = await scope.ServiceProvider.GetRequiredService<ISyncRunRepository>()
                .GetPendingDueAsync(clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }

        foreach (var run in due)
        {
            await using var scope = scopes.CreateAsyncScope();

            // Claim first so two instances never check (and download) the same report at once.
            // GetPendingDueAsync only returns runs with a check time.
            var claimed = await scope.ServiceProvider.GetRequiredService<ISyncRunRepository>()
                .TryClaimCheckAsync(run.Id, run.NextCheckAt!.Value, clock.GetUtcNow() + CheckLease, cancellationToken).ConfigureAwait(false);
            if (claimed)
            {
                await scope.ServiceProvider.GetRequiredService<IReportIngestionService>()
                    .ContinueAsync(run.Id, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RunDueSchedulesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SyncSchedule> due;
        await using (var scope = scopes.CreateAsyncScope())
        {
            // Pausing only stops scheduled slots; "Run now" and backfills are dequeued separately.
            // Overdue slots run once on resume, like after downtime.
            if ((await scope.ServiceProvider.GetRequiredService<ISyncSettingsRepository>().GetAsync(cancellationToken).ConfigureAwait(false)).IsPaused)
            {
                return;
            }

            due = await scope.ServiceProvider.GetRequiredService<ISyncScheduleRepository>()
                .GetDueAsync(clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }

        foreach (var schedule in due)
        {
            var now = clock.GetUtcNow();
            var next = ScheduleCalculator.NextRunAfter(schedule, now);

            bool claimed;
            bool stillWaiting = false;
            await using (var scope = scopes.CreateAsyncScope())
            {
                claimed = await scope.ServiceProvider.GetRequiredService<ISyncScheduleRepository>()
                    .TryClaimAsync(schedule.Id, schedule.NextRunAt!.Value, next, now, cancellationToken).ConfigureAwait(false);

                // The slot is consumed either way, so a slow Amazon report can't stack up runs for the
                // same schedule; the waiting run already covers this window and the next slot continues it.
                if (claimed)
                {
                    stillWaiting = await scope.ServiceProvider.GetRequiredService<ISyncRunRepository>()
                        .HasPendingAsync(schedule.Id, cancellationToken).ConfigureAwait(false);
                }
            }

            if (stillWaiting)
            {
                LogSkippedWhileWaiting(schedule.Name);
            }
            else if (claimed)
            {
                await RunAsync(schedule.Id, SyncTrigger.Scheduled, SchedulerUser, backfill: null, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RunAsync(int scheduleId, SyncTrigger trigger, string triggeredBy, BackfillWindow? backfill, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IReportIngestionService>()
            .RunAsync(scheduleId, trigger, triggeredBy, backfill, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync scheduler started (SP-API mode {Mode}, max sleep {MaxSleepMinutes} min)")]
    private partial void LogStarted(string mode, double maxSleepMinutes);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sync scheduler sleeping for {SleepMinutes:0.#} min")]
    private partial void LogSleeping(double sleepMinutes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped a scheduled run of '{ScheduleName}': its previous run is still waiting for Amazon")]
    private partial void LogSkippedWhileWaiting(string scheduleName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Marked {Count} interrupted sync run(s) as failed")]
    private partial void LogAbandoned(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync scheduler disabled on this instance (Ingestion:SchedulerEnabled = false)")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Error, Message = "Sync scheduler loop error; retrying in a minute")]
    private partial void LogLoopError(Exception exception);
}
