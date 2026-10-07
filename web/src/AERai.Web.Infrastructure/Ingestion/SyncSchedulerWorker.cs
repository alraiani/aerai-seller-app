using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.Ingestion;

/// <summary>
/// Background service that runs due schedules and "Run now" requests, one at a time.
/// </summary>
/// <remarks>
/// <para>Runs are sequential on purpose: SP-API quotas are per seller, so parallel pulls would only
/// queue behind each other in the rate limiter.</para>
/// <para>Each due slot is claimed atomically in the database before running, so several app
/// instances never run the same slot twice. After downtime, an overdue schedule runs once and then
/// resumes its normal cadence — missed slots are not replayed (Orders windows continue from the
/// last successful run, so no data is skipped).</para>
/// </remarks>
/// <param name="scopes">Creates a DI scope per run (services are scoped to the DbContext).</param>
/// <param name="channel">Manual-run requests.</param>
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

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.SchedulerEnabled)
        {
            LogDisabled();
            return;
        }

        LogStarted(connection.Mode, settings.SchedulerTick.TotalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await channel.DequeueAsync(settings.SchedulerTick, stoppingToken).ConfigureAwait(false) is { } manual)
                {
                    await RunAsync(manual.ScheduleId, SyncTrigger.Manual, manual.RequestedBy, manual.Backfill, stoppingToken).ConfigureAwait(false);
                }

                await RunDueSchedulesAsync(stoppingToken).ConfigureAwait(false);
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
                await Task.Delay(settings.SchedulerTick, clock, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RunDueSchedulesAsync(CancellationToken cancellationToken)
    {
        if (!connection.CanRun)
        {
            return;
        }

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
            await using (var scope = scopes.CreateAsyncScope())
            {
                claimed = await scope.ServiceProvider.GetRequiredService<ISyncScheduleRepository>()
                    .TryClaimAsync(schedule.Id, schedule.NextRunAt!.Value, next, now, cancellationToken).ConfigureAwait(false);
            }

            if (claimed)
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync scheduler started (SP-API mode {Mode}, tick {TickSeconds}s)")]
    private partial void LogStarted(string mode, double tickSeconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync scheduler disabled on this instance (Ingestion:SchedulerEnabled = false)")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Error, Message = "Sync scheduler loop error; retrying after one tick")]
    private partial void LogLoopError(Exception exception);
}
