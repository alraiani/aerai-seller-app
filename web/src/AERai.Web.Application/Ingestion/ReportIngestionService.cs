using System.Globalization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Imports;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Domain.Staging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Default <see cref="IReportIngestionService"/>. Two strategies, chosen by report type:
/// <list type="bullet">
///   <item><b>Requested</b> (Orders, FBA inventory): ask Amazon to generate a report, poll until it
///   is ready, then download it.</item>
///   <item><b>Listed</b> (Settlements): Amazon generates these itself, so list completed reports
///   and ingest each one not already in the <see cref="IngestedReport"/> ledger.</item>
/// </list>
/// Either way every document is landed in raw blob storage and staged through
/// <see cref="IStagingImportService.StageRawFileAsync"/> — the same path as a manual upload.
/// </summary>
public sealed partial class ReportIngestionService : IReportIngestionService
{
    /// <summary>
    /// Orders windows start this far before the previous run's end. Amazon can record updates late,
    /// and promotion is idempotent, so a small overlap costs nothing and prevents gaps.
    /// </summary>
    private static readonly TimeSpan OrdersOverlap = TimeSpan.FromMinutes(15);

    /// <summary>SP-API rejects a data end time in the future; stay safely behind "now".</summary>
    private static readonly TimeSpan DataEndSafetyMargin = TimeSpan.FromMinutes(2);

    /// <summary>Settlement listing re-checks a day of overlap; the ledger prevents duplicates.</summary>
    private static readonly TimeSpan SettlementListOverlap = TimeSpan.FromDays(1);

    private readonly IAmazonReportsGateway _gateway;
    private readonly IRawFileStore _rawFiles;
    private readonly IStagingImportService _staging;
    private readonly IPromotionService _promotion;
    private readonly ISyncScheduleRepository _schedules;
    private readonly ISyncRunRepository _runs;
    private readonly IngestionOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReportIngestionService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="gateway">SP-API Reports gateway.</param>
    /// <param name="rawFiles">Raw landing zone.</param>
    /// <param name="staging">Staging use cases.</param>
    /// <param name="promotion">Promotion into core.</param>
    /// <param name="schedules">Schedule persistence.</param>
    /// <param name="runs">Run history persistence.</param>
    /// <param name="options">Polling settings.</param>
    /// <param name="clock">Clock (also drives polling delays, so tests run instantly).</param>
    /// <param name="logger">Logger.</param>
    public ReportIngestionService(
        IAmazonReportsGateway gateway,
        IRawFileStore rawFiles,
        IStagingImportService staging,
        IPromotionService promotion,
        ISyncScheduleRepository schedules,
        ISyncRunRepository runs,
        IOptions<IngestionOptions> options,
        TimeProvider clock,
        ILogger<ReportIngestionService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _gateway = gateway;
        _rawFiles = rawFiles;
        _staging = staging;
        _promotion = promotion;
        _schedules = schedules;
        _runs = runs;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SyncRunSummary> RunAsync(int scheduleId, SyncTrigger trigger, string triggeredBy, BackfillWindow? backfill, CancellationToken cancellationToken)
    {
        var schedule = await _schedules.GetAsync(scheduleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Sync schedule {scheduleId} does not exist.");

        var run = new SyncRun
        {
            SyncScheduleId = schedule.Id,
            ReportType = schedule.ReportType,
            MarketplaceId = schedule.MarketplaceId,
            Trigger = trigger,
            TriggeredBy = triggeredBy,
            StartedAt = _clock.GetUtcNow(),
        };
        run.Id = await _runs.StartAsync(run, cancellationToken).ConfigureAwait(false);
        LogRunStarted(run.Id, schedule.Name, trigger);
        if (backfill is not null)
        {
            LogBackfill(run.Id, backfill.Start, backfill.End);
        }

        var context = new RunContext(schedule, run, backfill);
        try
        {
            if (schedule.ReportType == AmazonReportType.Settlements)
            {
                await IngestListedReportsAsync(context, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await IngestRequestedReportAsync(context, cancellationToken).ConfigureAwait(false);
            }

            if (run.Status == SyncRunStatus.Running)
            {
                run.Status = context.BatchIds.Count > 0 ? SyncRunStatus.Succeeded : SyncRunStatus.NoData;
            }

            if (run.Status is SyncRunStatus.Succeeded or SyncRunStatus.NoData && context.CoveredUntil is { } coveredUntil)
            {
                await _schedules.RecordSuccessAsync(schedule.Id, coveredUntil, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Fail(run, "Cancelled because the application is shutting down.");
        }
#pragma warning disable CA1031 // A background run must record any failure in its history instead of crashing the scheduler.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogRunFailed(ex, run.Id, schedule.Name);
            Fail(run, ex.Message);
        }

        run.CompletedAt = _clock.GetUtcNow();
        if (backfill is not null && run.Status != SyncRunStatus.Failed)
        {
            context.Notes.Insert(0, $"Backfill {backfill}.");
        }

        run.Message ??= string.Join(" ", context.Notes);
        run.AmazonReportIds = context.ReportIds.Count > 0 ? string.Join(",", context.ReportIds) : null;
        run.ImportBatchIds = context.BatchIds.Count > 0 ? string.Join(",", context.BatchIds) : null;

        // Persist the outcome even when shutdown cancelled the run itself.
        await _runs.CompleteAsync(run, CancellationToken.None).ConfigureAwait(false);
        LogRunCompleted(run.Id, run.Status, run.Message);

        return new SyncRunSummary(run.Id, run.Status, run.Message, context.BatchIds);
    }

    /// <summary>Request → poll → download one on-demand report.</summary>
    private async Task IngestRequestedReportAsync(RunContext context, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        DateTimeOffset? start = null;
        DateTimeOffset? end = null;

        if (context.Schedule.ReportType == AmazonReportType.Orders)
        {
            var latestEnd = now - DataEndSafetyMargin;
            end = context.Backfill is { } window && window.End < latestEnd ? window.End : latestEnd;
            start = context.Backfill is { } backfill
                ? backfill.Start
                : context.Schedule.LastSuccessfulDataEnd is { } previousEnd
                    ? previousEnd - OrdersOverlap
                    : end.Value.AddDays(-context.Schedule.LookbackDays);
        }

        context.Run.DataStart = start;
        context.Run.DataEnd = end;

        var reportId = await _gateway.RequestReportAsync(context.Schedule.ReportType, start, end, cancellationToken).ConfigureAwait(false);
        context.ReportIds.Add(reportId);

        var status = await WaitForReportAsync(reportId, cancellationToken).ConfigureAwait(false);
        switch (status.Status)
        {
            case AmazonProcessingStatus.Done when status.ReportDocumentId is not null:
                await LandAndStageAsync(context, reportId, status.ReportDocumentId, cancellationToken).ConfigureAwait(false);
                context.CoveredUntil = end ?? now;
                break;

            case AmazonProcessingStatus.Cancelled:
                // For on-demand reports Amazon cancels rather than returning an empty file when there's no data.
                context.Notes.Add("Amazon had no data for this window.");
                context.CoveredUntil = end ?? now;
                break;

            default:
                Fail(context.Run, $"Amazon could not generate report {reportId} (status {status.Status}).");
                break;
        }
    }

    /// <summary>List completed reports since the last run and ingest the new ones.</summary>
    private async Task IngestListedReportsAsync(RunContext context, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var since = context.Backfill is { } backfill
            ? backfill.Start
            : context.Schedule.LastSuccessfulDataEnd is { } previous
                ? previous - SettlementListOverlap
                : now.AddDays(-context.Schedule.LookbackDays);

        var reports = await _gateway.ListCompletedReportsAsync(context.Schedule.ReportType, since, cancellationToken).ConfigureAwait(false);
        var until = context.Backfill?.End ?? now;

        // A date-range backfill only takes reports Amazon created inside the range.
        reports = [.. reports.Where(r => r.CreatedAt <= until)];
        var skipped = 0;
        foreach (var report in reports)
        {
            if (await _runs.IsReportIngestedAsync(report.ReportId, cancellationToken).ConfigureAwait(false))
            {
                skipped++;
                continue;
            }

            context.ReportIds.Add(report.ReportId);
            await LandAndStageAsync(context, report.ReportId, report.ReportDocumentId, cancellationToken).ConfigureAwait(false);
        }

        if (context.BatchIds.Count == 0)
        {
            context.Notes.Add(skipped > 0 ? $"No new reports ({skipped} already ingested)." : "No new reports available.");
        }

        context.CoveredUntil = until;
    }

    /// <summary>Polls a report's status until it leaves the queue or the wait limit is reached.</summary>
    private async Task<AmazonReportStatus> WaitForReportAsync(string reportId, CancellationToken cancellationToken)
    {
        var deadline = _clock.GetUtcNow() + _options.ReportMaxWait;
        while (true)
        {
            var status = await _gateway.GetReportStatusAsync(reportId, cancellationToken).ConfigureAwait(false);
            if (status.Status is not (AmazonProcessingStatus.InQueue or AmazonProcessingStatus.InProgress))
            {
                return status;
            }

            if (_clock.GetUtcNow() >= deadline)
            {
                throw new TimeoutException($"Report {reportId} was not ready after {_options.ReportMaxWait.TotalMinutes:0} minutes.");
            }

            await Task.Delay(_options.ReportPollInterval, _clock, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Downloads a document, lands it in raw storage, stages it, and optionally promotes it.</summary>
    private async Task LandAndStageAsync(RunContext context, string reportId, string documentId, CancellationToken cancellationToken)
    {
        var schedule = context.Schedule;
        var source = ToImportSource(schedule.ReportType);
        var displayName = string.Create(CultureInfo.InvariantCulture, $"{schedule.ReportType}-{reportId}.tsv");
        var path = RawFilePaths.Build(source, _clock.GetUtcNow(), Guid.NewGuid(), displayName);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["source"] = source.ToString(),
            ["marketplaceid"] = schedule.MarketplaceId,
            ["amazonreportid"] = reportId,
            ["syncrunid"] = context.Run.Id.ToString(CultureInfo.InvariantCulture),
            ["uploadedby"] = context.Run.TriggeredBy,
        };

        string sha256;
        var document = await _gateway.OpenReportDocumentAsync(documentId, cancellationToken).ConfigureAwait(false);
        await using (document.ConfigureAwait(false))
        {
            sha256 = await _rawFiles.SaveAsync(path, document, metadata, cancellationToken).ConfigureAwait(false);
        }

        var staged = await _staging.StageRawFileAsync(
            new StageRawFileCommand(source, schedule.MarketplaceId, path, sha256, displayName, context.Run.TriggeredBy), cancellationToken).ConfigureAwait(false);

        if (staged.IsFailure)
        {
            if (staged.Error == StagingImportService.NoDataRowsError)
            {
                context.Notes.Add($"Report {reportId} was empty.");
                return;
            }

            throw new InvalidOperationException($"Report {reportId} could not be staged: {staged.Error}");
        }

        var batchId = staged.Value.BatchId;
        context.BatchIds.Add(batchId);
        await _runs.AddIngestedReportAsync(new IngestedReport
        {
            AmazonReportId = reportId,
            ReportType = schedule.ReportType,
            ImportBatchId = batchId,
            SyncRunId = context.Run.Id,
            IngestedAt = _clock.GetUtcNow(),
        }, cancellationToken).ConfigureAwait(false);

        if (!schedule.AutoPromote)
        {
            context.Notes.Add($"Staged batch {batchId} ({staged.Value.RowCount:N0} rows); awaiting promotion.");
            return;
        }

        var promoted = await _promotion.PromoteAsync(batchId, cancellationToken).ConfigureAwait(false);
        context.Notes.Add(promoted.IsSuccess
            ? $"Batch {batchId}: {promoted.Value.PromotedRowCount:N0} promoted, {promoted.Value.RejectedRowCount:N0} rejected."
            : $"Batch {batchId} staged but promotion failed: {promoted.Error}");
    }

    /// <summary>Each report type lands in the staging table that matches its native layout.</summary>
    private static ImportSource ToImportSource(AmazonReportType reportType) => reportType switch
    {
        AmazonReportType.Orders => ImportSource.Orders,
        AmazonReportType.FbaInventory => ImportSource.FbaInventory,
        AmazonReportType.Settlements => ImportSource.Settlements,
        _ => throw new InvalidOperationException($"No staging source for report type '{reportType}'."),
    };

    private static void Fail(SyncRun run, string message)
    {
        run.Status = SyncRunStatus.Failed;
        run.Message = message;
    }

    /// <summary>Mutable state accumulated during one run.</summary>
    private sealed class RunContext(SyncSchedule schedule, SyncRun run, BackfillWindow? backfill)
    {
        public BackfillWindow? Backfill { get; } = backfill;

        public SyncSchedule Schedule { get; } = schedule;

        public SyncRun Run { get; } = run;

        public List<string> ReportIds { get; } = [];

        public List<long> BatchIds { get; } = [];

        public List<string> Notes { get; } = [];

        public DateTimeOffset? CoveredUntil { get; set; }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync run {RunId} started for schedule '{ScheduleName}' ({Trigger})")]
    private partial void LogRunStarted(long runId, string scheduleName, SyncTrigger trigger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync run {RunId} is a backfill from {BackfillStart} to {BackfillEnd}")]
    private partial void LogBackfill(long runId, DateTimeOffset backfillStart, DateTimeOffset backfillEnd);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync run {RunId} finished: {Status} — {Message}")]
    private partial void LogRunCompleted(long runId, SyncRunStatus status, string? message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sync run {RunId} for schedule '{ScheduleName}' failed")]
    private partial void LogRunFailed(Exception exception, long runId, string scheduleName);
}
