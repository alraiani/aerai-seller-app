using System.Globalization;
using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Imports;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Domain.Staging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Default <see cref="IReportIngestionService"/>. Three strategies, chosen by report type:
/// <list type="bullet">
///   <item><b>Requested</b> (Orders, FBA inventory, restock): ask Amazon to generate a report and
///   record it on the run as pending; <see cref="ContinueAsync"/> later checks on it, with a growing
///   delay between checks, and downloads it once ready. Nothing waits in memory, so the scheduler is
///   free in the meantime and a restart resumes the run.</item>
///   <item><b>Listed</b> (Settlements): Amazon generates these itself, so list completed reports
///   and ingest each one not already in the <see cref="IngestedReport"/> ledger.</item>
///   <item><b>Listed by API</b> (AWD inventory): page through Amazon's JSON listing inside
///   <see cref="RunAsync"/>, land each page in raw storage, and replace the day's AWD snapshot.</item>
///   <item><b>Searched by API</b> (Orders API): page through <c>searchOrders</c> inside <see cref="RunAsync"/>,
///   land each page in raw storage, and upsert its orders straight into <c>core</c> as each page arrives.</item>
/// </list>
/// Report documents are landed in raw blob storage and staged through
/// <see cref="IStagingImportService.StageRawFileAsync"/> — the same path as a manual upload.
/// </summary>
public sealed partial class ReportIngestionService : IReportIngestionService
{
    /// <summary>
    /// Orders windows start this far before the previous run's end. Amazon can record updates late,
    /// and promotion is idempotent, so a small overlap costs nothing and prevents gaps.
    /// </summary>
    private static readonly TimeSpan OrdersOverlap = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How far behind "now" a window may end. Amazon rejects an end less than 2 minutes before <em>its</em> clock
    /// (a 400 on <c>createdBefore</c>), so stopping exactly 2 minutes behind ours fails whenever the clocks differ
    /// by a second; 5 minutes leaves room for that without losing anything the next run's overlap does not re-fetch.
    /// </summary>
    public static readonly TimeSpan DataEndSafetyMargin = TimeSpan.FromMinutes(5);

    /// <summary>How much of a failed report's error document is read; Amazon's are a line or two.</summary>
    private const int MaxFailureDocumentChars = 4_000;

    /// <summary>Settlement listing re-checks a day of overlap; the ledger prevents duplicates.</summary>
    private static readonly TimeSpan SettlementListOverlap = TimeSpan.FromDays(1);

    /// <summary>
    /// How far back Amazon will list reports (<c>getReports</c> rejects a <c>createdSince</c> older than 90
    /// days); one day inside the limit so clock and time-zone differences never tip a request over it.
    /// </summary>
    public static readonly TimeSpan MaxListingAge = TimeSpan.FromDays(89);

    /// <summary>Raw landing-zone folder for AWD inventory pages.</summary>
    public const string AwdRawFolder = "awdinventory";

    /// <summary>
    /// Stops a run whose next-page tokens never end (an Amazon fault) instead of looping forever.
    /// At 200 SKUs a page this allows 100,000 SKUs.
    /// </summary>
    private const int MaxAwdPages = 500;

    /// <summary>Raw landing-zone folder for Orders API pages.</summary>
    public const string OrdersApiRawFolder = "ordersapi";

    /// <summary>
    /// Stops a run whose next-page tokens never end (an Amazon fault) instead of looping forever.
    /// At 100 orders a page this allows 200,000 orders.
    /// </summary>
    private const int MaxOrdersPages = 2_000;

    private readonly IAmazonReportsGateway _gateway;
    private readonly IAmazonAwdGateway _awdGateway;
    private readonly IAwdInventoryRepository _awdInventory;
    private readonly IAmazonOrdersGateway _ordersGateway;
    private readonly IAmazonOrderRepository _apiOrders;
    private readonly IRawFileStore _rawFiles;
    private readonly IStagingImportService _staging;
    private readonly IPromotionService _promotion;
    private readonly ISyncScheduleRepository _schedules;
    private readonly ISyncRunRepository _runs;
    private readonly IMarketplaceQueries _marketplaces;
    private readonly IngestionOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReportIngestionService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="gateway">SP-API Reports gateway.</param>
    /// <param name="awdGateway">SP-API AWD inventory gateway.</param>
    /// <param name="awdInventory">AWD snapshot persistence.</param>
    /// <param name="ordersGateway">SP-API Orders API gateway.</param>
    /// <param name="apiOrders">Orders API order persistence.</param>
    /// <param name="rawFiles">Raw landing zone.</param>
    /// <param name="staging">Staging use cases.</param>
    /// <param name="promotion">Promotion into core.</param>
    /// <param name="schedules">Schedule persistence.</param>
    /// <param name="runs">Run history persistence.</param>
    /// <param name="marketplaces">Marketplace lookups (each schedule pulls for one marketplace).</param>
    /// <param name="options">Polling settings.</param>
    /// <param name="clock">Clock (also drives polling delays, so tests run instantly).</param>
    /// <param name="logger">Logger.</param>
    public ReportIngestionService(
        IAmazonReportsGateway gateway,
        IAmazonAwdGateway awdGateway,
        IAwdInventoryRepository awdInventory,
        IAmazonOrdersGateway ordersGateway,
        IAmazonOrderRepository apiOrders,
        IRawFileStore rawFiles,
        IStagingImportService staging,
        IPromotionService promotion,
        ISyncScheduleRepository schedules,
        ISyncRunRepository runs,
        IMarketplaceQueries marketplaces,
        IOptions<IngestionOptions> options,
        TimeProvider clock,
        ILogger<ReportIngestionService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _gateway = gateway;
        _awdGateway = awdGateway;
        _awdInventory = awdInventory;
        _ordersGateway = ordersGateway;
        _apiOrders = apiOrders;
        _rawFiles = rawFiles;
        _staging = staging;
        _promotion = promotion;
        _schedules = schedules;
        _runs = runs;
        _marketplaces = marketplaces;
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
            BackfillStart = backfill?.Start,
            BackfillEnd = backfill?.End,
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
            context.Marketplace = await ResolveMarketplaceAsync(schedule, cancellationToken).ConfigureAwait(false);
            if (schedule.ReportType == AmazonReportType.AwdInventory)
            {
                await IngestAwdInventoryAsync(context, cancellationToken).ConfigureAwait(false);
            }
            else if (schedule.ReportType == AmazonReportType.OrdersApi)
            {
                await IngestOrdersApiAsync(context, cancellationToken).ConfigureAwait(false);
            }
            else if (schedule.ReportType == AmazonReportType.Settlements)
            {
                await IngestListedReportsAsync(context, cancellationToken).ConfigureAwait(false);
            }
            else if (await RequestReportAsync(context, cancellationToken).ConfigureAwait(false))
            {
                return Summarize(context);
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

        return await FinishAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SyncRunSummary> ContinueAsync(long runId, CancellationToken cancellationToken)
    {
        var run = await _runs.GetAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Sync run {runId} does not exist.");
        if (run.Status != SyncRunStatus.Running || run.NextCheckAt is null)
        {
            return new SyncRunSummary(run.Id, run.Status, run.Message ?? string.Empty, []);
        }

        // A schedule deleted while Amazon was generating its report still finishes the run, so the
        // report Amazon already made is not wasted.
        var schedule = await _schedules.GetIncludingDeletedAsync(run.SyncScheduleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Sync schedule {run.SyncScheduleId} does not exist.");
        var backfill = run.BackfillStart is { } start && run.BackfillEnd is { } end ? new BackfillWindow(start, end) : null;
        var context = new RunContext(schedule, run, backfill);

        try
        {
            context.Marketplace = await ResolveMarketplaceAsync(schedule, cancellationToken).ConfigureAwait(false);
            if (run.PendingReportId is not { } reportId)
            {
                // Held back by the report's request cooldown: ask Amazon now (or wait again if another run asked meanwhile).
                if (await RequestReportAsync(context, cancellationToken).ConfigureAwait(false))
                {
                    return Summarize(context);
                }

                return await FinishAsync(context, cancellationToken).ConfigureAwait(false);
            }

            context.ReportIds.Add(reportId);
            var status = await _gateway.GetReportStatusAsync(context.Marketplace, reportId, cancellationToken).ConfigureAwait(false);
            if (status.Status is AmazonProcessingStatus.InQueue or AmazonProcessingStatus.InProgress)
            {
                var now = _clock.GetUtcNow();
                if (now >= (run.ReportRequestedAt ?? run.StartedAt) + _options.ReportMaxWait)
                {
                    throw new TimeoutException($"Report {reportId} was not ready after {_options.ReportMaxWait.TotalMinutes:0} minutes.");
                }

                run.PollAttempts++;
                run.NextCheckAt = now + PollDelay(run.PollAttempts);
                await _runs.SaveProgressAsync(run, cancellationToken).ConfigureAwait(false);
                return Summarize(context);
            }

            await IngestFinishedReportAsync(context, reportId, status, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down mid-check: leave the run waiting and due now, so the next start resumes it
            // straight away instead of after the claim lease.
            run.NextCheckAt = _clock.GetUtcNow();
            await _runs.SaveProgressAsync(run, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
#pragma warning disable CA1031 // A background run must record any failure in its history instead of crashing the scheduler.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogRunFailed(ex, run.Id, schedule.Name);
            Fail(run, ex.Message);
        }

        return await FinishAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Delay before the next status check: <see cref="IngestionOptions.ReportPollInterval"/> doubled
    /// for every check that found the report still being generated, capped at
    /// <see cref="IngestionOptions.ReportPollMaxInterval"/>. Most reports are ready within a few
    /// minutes, so early checks are frequent and a slow report costs few calls.
    /// </summary>
    private TimeSpan PollDelay(int attempts)
    {
        var max = _options.ReportPollMaxInterval > _options.ReportPollInterval ? _options.ReportPollMaxInterval : _options.ReportPollInterval;
        var delay = _options.ReportPollInterval;
        for (var i = 0; i < attempts && delay < max; i++)
        {
            delay += delay;
        }

        return delay > max ? max : delay;
    }

    /// <summary>
    /// Records the outcome of a run that is no longer waiting on Amazon and moves the schedule's
    /// last-successful marker forward.
    /// </summary>
    private async Task<SyncRunSummary> FinishAsync(RunContext context, CancellationToken cancellationToken)
    {
        var run = context.Run;
        if (run.Status == SyncRunStatus.Running)
        {
            run.Status = context.BatchIds.Count > 0 || context.RowsWritten > 0 ? SyncRunStatus.Succeeded : SyncRunStatus.NoData;
        }

        if (run.Status is SyncRunStatus.Succeeded or SyncRunStatus.NoData && context.CoveredUntil is { } coveredUntil)
        {
            await _schedules.RecordSuccessAsync(context.Schedule.Id, coveredUntil, cancellationToken).ConfigureAwait(false);
        }

        run.CompletedAt = _clock.GetUtcNow();
        if (context.Backfill is { } backfill && run.Status != SyncRunStatus.Failed)
        {
            context.Notes.Insert(0, $"Backfill {backfill}.");
        }

        run.Message ??= string.Join(" ", context.Notes);
        run.AmazonReportIds = context.ReportIds.Count > 0 ? string.Join(",", context.ReportIds) : null;
        run.ImportBatchIds = context.BatchIds.Count > 0 ? string.Join(",", context.BatchIds) : null;
        run.PendingReportId = null;
        run.NextCheckAt = null;

        // Persist the outcome even when shutdown cancelled the run itself.
        await _runs.CompleteAsync(run, CancellationToken.None).ConfigureAwait(false);
        LogRunCompleted(run.Id, run.Status, run.Message);

        return Summarize(context);
    }

    private static SyncRunSummary Summarize(RunContext context) =>
        new(context.Run.Id, context.Run.Status, context.Run.Message ?? string.Join(" ", context.Notes), context.BatchIds);

    /// <summary>
    /// Asks Amazon to generate an on-demand report and records it on the run as pending. The run
    /// stays <see cref="SyncRunStatus.Running"/>; the scheduler checks on it later through
    /// <see cref="ContinueAsync"/> instead of waiting here.
    /// </summary>
    /// <remarks>
    /// A report asked for again inside its <see cref="AmazonReportTypeExtensions.RequestCooldown"/> is not
    /// sent: the run waits until the cooldown ends and <see cref="ContinueAsync"/> asks then. If another run of
    /// the schedule is already waiting to ask, this one ends without asking, since that run's report will
    /// be just as fresh.
    /// </remarks>
    /// <returns><see langword="true"/> when the run is now waiting; <see langword="false"/> when it is done (skipped).</returns>
    private async Task<bool> RequestReportAsync(RunContext context, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var run = context.Run;
        if (await RequestAllowedAtAsync(context, now, cancellationToken).ConfigureAwait(false) is { } allowedAt)
        {
            if (await _runs.HasWaitingRequestAsync(context.Schedule.Id, run.Id, cancellationToken).ConfigureAwait(false))
            {
                context.Notes.Add($"Not requested: another run of this schedule asks Amazon for the report at the end of its {context.Schedule.ReportType.RequestCooldown().TotalMinutes:0}-minute limit.");
                LogRequestSkipped(run.Id, context.Schedule.Name);
                return false;
            }

            run.NextCheckAt = allowedAt;
            run.Message = ReportRequestCooldown.Describe(context.Schedule.ReportType, allowedAt);
            await _runs.SaveProgressAsync(run, cancellationToken).ConfigureAwait(false);
            LogRequestHeldBack(run.Id, context.Schedule.Name, allowedAt);
            return true;
        }

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

        run.DataStart = start;
        run.DataEnd = end;

        var reportId = await _gateway.RequestReportAsync(context.Marketplace, context.Schedule.ReportType, start, end, cancellationToken).ConfigureAwait(false);
        context.ReportIds.Add(reportId);

        run.AmazonReportIds = reportId;
        run.PendingReportId = reportId;

        // A run that waited out the cooldown no longer waits; its outcome becomes the message when it finishes.
        run.Message = null;
        run.ReportRequestedAt = now;
        run.PollAttempts = 0;
        run.NextCheckAt = now + PollDelay(0);
        await _runs.SaveProgressAsync(run, cancellationToken).ConfigureAwait(false);
        LogAwaitingReport(run.Id, reportId, run.NextCheckAt.Value);
        return true;
    }

    /// <summary>
    /// When the report may next be asked for, if that is still in the future: the last request for the
    /// same report type and marketplace (by any run) plus the report's cooldown and a margin for clock
    /// differences with Amazon. <see langword="null"/> when it may be asked for now.
    /// </summary>
    private async Task<DateTimeOffset?> RequestAllowedAtAsync(RunContext context, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reportType = context.Schedule.ReportType;
        if (reportType.RequestCooldown() <= TimeSpan.Zero)
        {
            return null;
        }

        var lastRequest = await _runs.GetLastReportRequestAsync(reportType, context.Schedule.MarketplaceId, cancellationToken).ConfigureAwait(false);
        return ReportRequestCooldown.WaitUntil(reportType, lastRequest, now);
    }

    /// <summary>Downloads (or records the absence of) an on-demand report Amazon has finished with.</summary>
    private async Task IngestFinishedReportAsync(RunContext context, string reportId, AmazonReportStatus status, CancellationToken cancellationToken)
    {
        // Snapshot reports have no data window; they cover up to when they were requested.
        var coveredUntil = context.Run.DataEnd ?? context.Run.StartedAt;
        switch (status.Status)
        {
            case AmazonProcessingStatus.Done when status.ReportDocumentId is not null:
                // A restart between staging and completing the run must not stage the same report twice.
                if (await _runs.IsReportIngestedAsync(reportId, cancellationToken).ConfigureAwait(false))
                {
                    context.Notes.Add($"Report {reportId} was already ingested.");
                }
                else
                {
                    await LandAndStageAsync(context, reportId, status.ReportDocumentId, cancellationToken).ConfigureAwait(false);
                }

                context.CoveredUntil = coveredUntil;
                break;

            case AmazonProcessingStatus.Cancelled:
                // For on-demand reports Amazon cancels rather than returning an empty file when there's no data.
                context.Notes.Add("Amazon had no data for this window.");
                context.CoveredUntil = coveredUntil;
                break;

            default:
                var reason = status.ReportDocumentId is { } errorDocumentId
                    ? await ReadFailureReasonAsync(context, reportId, errorDocumentId, cancellationToken).ConfigureAwait(false)
                    : null;
                Fail(context.Run, reason is null
                    ? $"Amazon could not generate report {reportId} (status {status.Status})."
                    : $"Amazon could not generate report {reportId} (status {status.Status}): {reason}");
                break;
        }
    }

    /// <summary>
    /// Reads the short error document Amazon attaches to a failed report, which says why it failed
    /// (usually JSON with an <c>errorDetails</c> field). Best effort: <see langword="null"/> when it cannot be read,
    /// so the run still fails with the plain message.
    /// </summary>
    private async Task<string?> ReadFailureReasonAsync(RunContext context, string reportId, string reportDocumentId, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await _gateway.OpenReportDocumentAsync(context.Marketplace, reportDocumentId, cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var buffer = new char[MaxFailureDocumentChars];
                var read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                return FailureReason.FromDocument(new string(buffer, 0, read));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // The reason only improves the failure message; not getting it must not change the outcome.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogFailureReasonUnreadable(ex, reportId);
            return null;
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

        // Amazon only lists reports from the last 90 days and rejects an older start with a 400. Start at the
        // oldest it will accept (a day inside the limit) and say so; older reports have to be uploaded.
        var oldest = now - MaxListingAge;
        if (since < oldest)
        {
            since = oldest;
            context.Notes.Add($"Amazon only lists reports from the last 90 days, so this pull starts at {oldest:yyyy-MM-dd}; for older ones, upload the files to raw storage and use Refresh from blobs.");
        }

        var reports = await _gateway.ListCompletedReportsAsync(context.Marketplace, context.Schedule.ReportType, since, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Pages through Amazon's AWD inventory, lands every page in raw storage, then replaces the day's
    /// AWD snapshot in one go.
    /// </summary>
    /// <remarks>
    /// All pages are read before anything is written, so a run that fails part-way leaves the
    /// previous snapshot whole instead of a partial one; a seller's AWD listing is small enough to
    /// hold in memory. SKUs whose quantities are all zero are not stored: Amazon keeps listing SKUs
    /// that have left AWD.
    /// </remarks>
    private async Task IngestAwdInventoryAsync(RunContext context, CancellationToken cancellationToken)
    {
        var startedAt = context.Run.StartedAt;
        var items = new Dictionary<string, AwdInventoryItem>(StringComparer.OrdinalIgnoreCase);
        string? nextToken = null;
        var pages = 0;
        do
        {
            if (++pages > MaxAwdPages)
            {
                throw new InvalidOperationException($"AWD inventory listing did not end after {MaxAwdPages} pages.");
            }

            var page = await _awdGateway.ListInventoryPageAsync(context.Marketplace, nextToken, cancellationToken).ConfigureAwait(false);
            await LandJsonPageAsync(context, AwdRawFolder, "awd-inventory", page.RawJson, pages, cancellationToken).ConfigureAwait(false);
            foreach (var item in page.Items.Where(i => !i.IsEmpty))
            {
                items[item.Sku] = item;
            }

            nextToken = string.IsNullOrEmpty(page.NextToken) ? null : page.NextToken;
        }
        while (nextToken is not null);

        // Dated by the run's UTC start, like the FBA snapshots are dated by their batch's UTC receive date.
        var snapshotDate = DateOnly.FromDateTime(startedAt.UtcDateTime);
        context.RowsWritten = await _awdInventory.ReplaceSnapshotAsync(
            context.Schedule.MarketplaceId, snapshotDate, items.Values, _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        context.Notes.Add(context.RowsWritten > 0
            ? $"AWD snapshot for {snapshotDate:yyyy-MM-dd}: {context.RowsWritten:N0} SKUs ({pages:N0} page{(pages == 1 ? "" : "s")})."
            : "Amazon lists no AWD stock.");
        context.CoveredUntil = startedAt;
    }

    /// <summary>
    /// Pages through the Orders API for the run's window, lands every page in raw storage, and upserts
    /// each page's orders as it arrives.
    /// </summary>
    /// <remarks>
    /// Unlike AWD, orders are written page by page rather than all at the end: orders are keyed by id and
    /// upserted, so a run that fails or is cancelled part-way keeps what earlier pages brought and the
    /// next run's overlap fills the rest. A scheduled run asks for orders updated since the last
    /// success (catching status changes on older orders); a backfill asks for orders created in its range.
    /// </remarks>
    private async Task IngestOrdersApiAsync(RunContext context, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var latestEnd = now - DataEndSafetyMargin;
        DateTimeOffset start;
        DateTimeOffset end;
        AmazonOrdersFilter filter;
        if (context.Backfill is { } window)
        {
            start = window.Start;
            end = window.End < latestEnd ? window.End : latestEnd;
            filter = new AmazonOrdersFilter(CreatedAfter: start, CreatedBefore: end, LastUpdatedAfter: null);
        }
        else
        {
            end = latestEnd;
            start = context.Schedule.LastSuccessfulDataEnd is { } previousEnd
                ? previousEnd - OrdersOverlap
                : end.AddDays(-context.Schedule.LookbackDays);
            filter = new AmazonOrdersFilter(CreatedAfter: null, CreatedBefore: null, LastUpdatedAfter: start);
        }

        context.Run.DataStart = start;
        context.Run.DataEnd = end;

        var inserted = 0;
        var updated = 0;
        var pages = 0;
        string? nextToken = null;
        do
        {
            if (++pages > MaxOrdersPages)
            {
                throw new InvalidOperationException($"The Orders API search did not end after {MaxOrdersPages} pages.");
            }

            var page = await _ordersGateway.SearchOrdersPageAsync(context.Marketplace, filter, nextToken, cancellationToken).ConfigureAwait(false);
            await LandJsonPageAsync(context, OrdersApiRawFolder, "orders", page.RawJson, pages, cancellationToken).ConfigureAwait(false);

            // One write per order id, even if Amazon repeats one within a page.
            var orders = page.Orders
                .GroupBy(o => o.AmazonOrderId, StringComparer.Ordinal)
                .Select(g => g.OrderByDescending(o => o.LastUpdatedAt).First())
                .ToList();
            if (orders.Count > 0)
            {
                var result = await _apiOrders.UpsertAsync(context.Schedule.MarketplaceId, orders, _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                inserted += result.Inserted;
                updated += result.Updated;
                context.RowsWritten += result.Total;
            }

            nextToken = string.IsNullOrEmpty(page.NextToken) ? null : page.NextToken;
        }
        while (nextToken is not null);

        context.Notes.Add(context.RowsWritten > 0
            ? $"Orders API: {context.RowsWritten:N0} order{(context.RowsWritten == 1 ? "" : "s")} ({inserted:N0} new, {updated:N0} updated) across {pages:N0} page{(pages == 1 ? "" : "s")}."
            : "Amazon returned no orders for this window.");
        context.CoveredUntil = end;
    }

    /// <summary>Keeps one JSON API response page in the raw landing zone, byte-for-byte as Amazon sent it.</summary>
    private async Task LandJsonPageAsync(RunContext context, string folder, string filePrefix, string rawJson, int pageNumber, CancellationToken cancellationToken)
    {
        var fileName = string.Create(CultureInfo.InvariantCulture, $"{filePrefix}-run{context.Run.Id}-page{pageNumber}.json");
        var path = RawFilePaths.BuildInFolder(folder, _clock.GetUtcNow(), Guid.NewGuid(), fileName);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["source"] = folder,
            ["marketplaceid"] = context.Schedule.MarketplaceId,
            ["syncrunid"] = context.Run.Id.ToString(CultureInfo.InvariantCulture),
            ["uploadedby"] = context.Run.TriggeredBy,
        };

        var content = new MemoryStream(Encoding.UTF8.GetBytes(rawJson), writable: false);
        await using (content.ConfigureAwait(false))
        {
            await _rawFiles.SaveAsync(path, content, metadata, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Downloads a document, lands it in raw storage, stages it, and optionally promotes it.</summary>
    private async Task LandAndStageAsync(RunContext context, string reportId, string documentId, CancellationToken cancellationToken)
    {
        var schedule = context.Schedule;
        var source = ToImportSource(schedule.ReportType);
        var marketplaceId = StagingMarketplaceId(context);
        var displayName = string.Create(CultureInfo.InvariantCulture, $"{schedule.ReportType}-{reportId}.tsv");
        var path = RawFilePaths.Build(source, _clock.GetUtcNow(), Guid.NewGuid(), displayName);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["source"] = source.ToString(),
            ["marketplaceid"] = marketplaceId,
            ["amazonreportid"] = reportId,
            ["syncrunid"] = context.Run.Id.ToString(CultureInfo.InvariantCulture),
            ["uploadedby"] = context.Run.TriggeredBy,
        };

        string sha256;
        var document = await _gateway.OpenReportDocumentAsync(context.Marketplace, documentId, cancellationToken).ConfigureAwait(false);
        await using (document.ConfigureAwait(false))
        {
            sha256 = await _rawFiles.SaveAsync(path, document, metadata, cancellationToken).ConfigureAwait(false);
        }

        var staged = await _staging.StageRawFileAsync(
            new StageRawFileCommand(source, marketplaceId, path, sha256, displayName, context.Run.TriggeredBy), cancellationToken).ConfigureAwait(false);

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
            ? $"Batch {batchId}: {promoted.Value.PromotedRowCount:N0} promoted, {promoted.Value.RejectedRowCount:N0} rejected{SkippedNote(promoted.Value)}."
            : $"Batch {batchId} staged but promotion failed: {promoted.Error}");
    }

    /// <summary>
    /// The marketplace a downloaded report is staged under: the schedule's, except that settlements go to
    /// the marketplace that keeps the schedule marketplace's payouts.
    /// </summary>
    /// <remarks>
    /// Amazon ignores the marketplace when listing settlement reports and returns every payout of the
    /// region (USD, CAD, and MXN in North America) whichever marketplace asks. They are all kept under the
    /// United States (Canada's <see cref="Marketplace.SettlementMarketplaceId"/>), so it never depends on
    /// which schedule happened to list a report first; each is converted into USD for reporting.
    /// </remarks>
    private static string StagingMarketplaceId(RunContext context) =>
        context.Schedule.ReportType == AmazonReportType.Settlements && context.Marketplace.SettlementMarketplaceId is { } settlementMarketplaceId
            ? settlementMarketplaceId
            : context.Schedule.MarketplaceId;

    /// <summary>Each report type lands in the staging table that matches its native layout.</summary>
    /// <summary>The schedule's marketplace, which must still exist and be active to be pulled.</summary>
    private async Task<Marketplace> ResolveMarketplaceAsync(SyncSchedule schedule, CancellationToken cancellationToken)
    {
        var all = await _marketplaces.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var marketplace = all.FirstOrDefault(m => m.MarketplaceId == schedule.MarketplaceId)
            ?? throw new InvalidOperationException($"Marketplace '{schedule.MarketplaceId}' does not exist.");
        return marketplace.IsActive
            ? marketplace
            : throw new InvalidOperationException($"{marketplace.Name} is not active, so its reports are not pulled.");
    }

    // An orders report covers its whole region, so other marketplaces' rows are routine, not errors.
    private static string SkippedNote(PromotionSummary summary) =>
        summary.SkippedRowCount > 0 ? $", {summary.SkippedRowCount:N0} for other marketplaces" : "";

    private static ImportSource ToImportSource(AmazonReportType reportType) => reportType switch
    {
        AmazonReportType.Orders => ImportSource.Orders,
        AmazonReportType.FbaInventory => ImportSource.FbaInventory,
        AmazonReportType.FbaReservedInventory => ImportSource.FbaReservedInventory,
        AmazonReportType.RestockRecommendations => ImportSource.FbaRestockRecommendations,
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

        /// <summary>The schedule's marketplace.</summary>
        // Assigned first thing in RunAsync, before any Amazon call reads it.
        public Marketplace Marketplace { get; set; } = default!;

        public List<string> ReportIds { get; } = [];

        public List<long> BatchIds { get; } = [];

        public List<string> Notes { get; } = [];

        public DateTimeOffset? CoveredUntil { get; set; }

        /// <summary>Rows written straight to <c>core</c> by runs that are not staged (AWD, Orders API).</summary>
        public int RowsWritten { get; set; }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync run {RunId} started for schedule '{ScheduleName}' ({Trigger})")]
    private partial void LogRunStarted(long runId, string scheduleName, SyncTrigger trigger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync run {RunId} is a backfill from {BackfillStart} to {BackfillEnd}")]
    private partial void LogBackfill(long runId, DateTimeOffset backfillStart, DateTimeOffset backfillEnd);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync run {RunId} is waiting for Amazon report {ReportId}; next check at {NextCheckAt}")]
    private partial void LogAwaitingReport(long runId, string reportId, DateTimeOffset nextCheckAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync run {RunId} ('{ScheduleName}') waits until {AllowedAt} to request its report: Amazon refuses it more often")]
    private partial void LogRequestHeldBack(long runId, string scheduleName, DateTimeOffset allowedAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync run {RunId} ('{ScheduleName}') did not request its report: another run of the schedule is already waiting to")]
    private partial void LogRequestSkipped(long runId, string scheduleName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the error document of failed Amazon report {ReportId}")]
    private partial void LogFailureReasonUnreadable(Exception exception, string reportId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync run {RunId} finished: {Status} — {Message}")]
    private partial void LogRunCompleted(long runId, SyncRunStatus status, string? message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sync run {RunId} for schedule '{ScheduleName}' failed")]
    private partial void LogRunFailed(Exception exception, long runId, string scheduleName);
}
