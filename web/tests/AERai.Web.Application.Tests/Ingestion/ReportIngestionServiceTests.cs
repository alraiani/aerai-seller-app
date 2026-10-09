using AERai.Web.Application.Imports;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Domain.Staging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Ingestion;

public sealed class ReportIngestionServiceTests
{
    private const string OrdersTsv =
        "amazon-order-id\tpurchase-date\torder-status\tsku\tquantity\titem-price\n" +
        "111-1\t2026-10-01T10:00:00Z\tShipped\tA-1\t2\t19.98\n";

    private const string SettlementTsv =
        "settlement-id\ttransaction-type\tamount-type\tamount\n" +
        "S1\tOrder\tItemPrice\t10.00\n";

    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeAmazonReportsGateway _gateway = new();
    private readonly FakeRawFileStore _rawFiles = new();
    private readonly FakeStagingRepository _staged = new();
    private readonly FakePromotionService _promotion = new();
    private readonly FakeSyncScheduleRepository _schedules = new();
    private readonly FakeSyncRunRepository _runs = new();
    private readonly FakeMarketplaceQueries _marketplaces = new();
    private readonly FakeTimeProvider _clock = new(Now);

    private ReportIngestionService CreateService(IngestionOptions? ingestion = null)
    {
        var staging = new StagingImportService(
            [new OrderLineMapper(), new SettlementLineMapper(), new FbaInventoryRowMapper(), new FbaReservedInventoryRowMapper()],
            _rawFiles, _staged, new FakeImportBatchQueries(), Options.Create(new ImportOptions()), _clock,
            NullLogger<StagingImportService>.Instance);

        // Zero poll interval by default: every check is due immediately, so RunToEndAsync needs no clock moves.
        var options = Options.Create(ingestion ?? new IngestionOptions { ReportPollInterval = TimeSpan.Zero, ReportMaxWait = TimeSpan.FromMinutes(5) });

        return new ReportIngestionService(_gateway, _rawFiles, staging, _promotion, _schedules, _runs, _marketplaces, options, _clock,
            NullLogger<ReportIngestionService>.Instance);
    }

    /// <summary>Starts a run of schedule 1 and keeps checking on it, as the scheduler would, until it finishes.</summary>
    private async Task<SyncRunSummary> RunToEndAsync(SyncTrigger trigger, string triggeredBy, BackfillWindow? backfill, CancellationToken cancellationToken)
    {
        var service = CreateService();
        var summary = await service.RunAsync(1, trigger, triggeredBy, backfill, cancellationToken);
        for (var checks = 0; summary.Status == SyncRunStatus.Running; checks++)
        {
            Assert.True(checks < 20, "The run never finished.");
            summary = await service.ContinueAsync(summary.RunId, cancellationToken);
        }

        return summary;
    }

    private SyncSchedule AddSchedule(
        AmazonReportType type, bool autoPromote = true, DateTimeOffset? lastEnd = null, string marketplaceId = MarketplaceIds.UnitedStates)
    {
        var schedule = new SyncSchedule
        {
            Id = 1, Name = "s", ReportType = type, MarketplaceId = marketplaceId,
            IsEnabled = true, Frequency = ScheduleFrequency.Interval, IntervalMinutes = 60,
            TimeZoneId = "UTC", LookbackDays = 7, AutoPromote = autoPromote, LastSuccessfulDataEnd = lastEnd, UpdatedBy = "t",
        };
        _schedules.Schedules[1] = schedule;
        return schedule;
    }

    private void ScriptDoneReport(string document)
    {
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.InQueue, null));
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.InProgress, null));
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.Done, "D1"));
        _gateway.Documents["D1"] = document;
    }

    [Fact]
    public async Task RunAsync_CanadaSchedule_PullsForCanadaAndStagesUnderCanada()
    {
        AddSchedule(AmazonReportType.Orders, marketplaceId: MarketplaceIds.Canada);
        ScriptDoneReport(OrdersTsv);

        var summary = await RunToEndAsync(SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Succeeded, summary.Status);
        Assert.Equal(MarketplaceIds.Canada, Assert.Single(_gateway.MarketplacesSeen));
        Assert.Equal(MarketplaceIds.Canada, Assert.Single(_staged.Saved).MarketplaceId);
        Assert.Equal(MarketplaceIds.Canada, Assert.Single(_runs.Completed).MarketplaceId);
    }

    [Fact]
    public async Task RunAsync_InactiveMarketplace_FailsWithoutCallingAmazon()
    {
        AddSchedule(AmazonReportType.Orders, marketplaceId: MarketplaceIds.UnitedKingdom);

        var summary = await RunToEndAsync(SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Failed, summary.Status);
        Assert.Contains("not active", summary.Message, StringComparison.Ordinal);
        Assert.Empty(_gateway.MarketplacesSeen);
    }

    [Fact]
    public async Task RunAsync_FirstOrdersRun_RequestsLookbackWindowAndLandsStagesAndPromotes()
    {
        AddSchedule(AmazonReportType.Orders);
        ScriptDoneReport(OrdersTsv);

        var summary = await RunToEndAsync(SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Succeeded, summary.Status);
        var request = Assert.Single(_gateway.Requests);
        Assert.Equal(Now.AddMinutes(-2).AddDays(-7), request.Start);
        Assert.Equal(Now.AddMinutes(-2), request.End);

        var (path, file) = Assert.Single(_rawFiles.Files);
        Assert.StartsWith("orders/2026/10/02/", path, StringComparison.Ordinal);
        Assert.Equal("R1", file.Metadata["amazonreportid"]);

        var batch = Assert.Single(_staged.Saved);
        Assert.Equal(path, batch.RawFilePath);
        Assert.Equal([batch.Id], _promotion.Promoted);
        Assert.Equal(request.End, _schedules.Schedules[1].LastSuccessfulDataEnd);
        Assert.Equal("R1", Assert.Single(_runs.Ledger).AmazonReportId);
    }

    [Fact]
    public async Task RunAsync_OtherMarketplacesRowsSkipped_NotesThemSeparatelyFromRejections()
    {
        AddSchedule(AmazonReportType.Orders);
        ScriptDoneReport(OrdersTsv);
        _promotion.SkippedRowCount = 2644;

        var summary = await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Succeeded, summary.Status);
        Assert.Contains("1 promoted, 0 rejected, 2,644 for other marketplaces.", summary.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_NoRowsSkipped_LeavesOtherMarketplacesOutOfTheNote()
    {
        AddSchedule(AmazonReportType.Orders);
        ScriptDoneReport(OrdersTsv);

        var summary = await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Contains("1 promoted, 0 rejected.", summary.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("other marketplaces", summary.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_LaterOrdersRun_ContinuesFromLastEndWithOverlap()
    {
        var lastEnd = Now.AddHours(-1);
        AddSchedule(AmazonReportType.Orders, lastEnd: lastEnd);
        ScriptDoneReport(OrdersTsv);

        await RunToEndAsync(SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(lastEnd.AddMinutes(-15), Assert.Single(_gateway.Requests).Start);
    }

    [Fact]
    public async Task RunAsync_OrdersBackfill_RequestsFullWindowInsteadOfContinuing()
    {
        AddSchedule(AmazonReportType.Orders, lastEnd: Now.AddHours(-1));
        ScriptDoneReport(OrdersTsv);

        var summary = await RunToEndAsync(SyncTrigger.Manual, "ops", BackfillWindow.LastDays(30, Now), CancellationToken.None);

        var request = Assert.Single(_gateway.Requests);
        Assert.Equal(Now.AddDays(-30), request.Start);
        Assert.StartsWith("Backfill 2026-09-02 to 2026-10-02.", summary.Message, StringComparison.Ordinal);
        Assert.Equal(request.End, _schedules.Schedules[1].LastSuccessfulDataEnd); // Moved forward, never back.
    }

    [Fact]
    public async Task RunAsync_SettlementsBackfill_ListsFromBackfillStart()
    {
        AddSchedule(AmazonReportType.Settlements, lastEnd: Now.AddHours(-1));
        _gateway.Available.Add(new AvailableAmazonReport("OLDER", "D-OLDER", Now.AddDays(-20)));
        _gateway.Documents["D-OLDER"] = SettlementTsv;

        var summary = await RunToEndAsync(SyncTrigger.Manual, "ops", BackfillWindow.LastDays(30, Now), CancellationToken.None);

        // Without the backfill, listing would start an hour ago (minus overlap) and miss this report.
        Assert.Equal(SyncRunStatus.Succeeded, summary.Status);
        Assert.Contains(_runs.Ledger, r => r.AmazonReportId == "OLDER");
    }

    [Fact]
    public async Task RunAsync_OrdersDateRangeBackfill_RequestsExactlyThatRange()
    {
        AddSchedule(AmazonReportType.Orders, lastEnd: Now.AddHours(-1));
        ScriptDoneReport(OrdersTsv);
        var window = new BackfillWindow(Now.AddDays(-25), Now.AddDays(-10));

        await RunToEndAsync(SyncTrigger.Manual, "ops", window, CancellationToken.None);

        var request = Assert.Single(_gateway.Requests);
        Assert.Equal(window.Start, request.Start);
        Assert.Equal(window.End, request.End);
        Assert.Equal(Now.AddHours(-1), _schedules.Schedules[1].LastSuccessfulDataEnd); // An older window never moves the marker back.
    }

    [Fact]
    public async Task RunAsync_SettlementsDateRangeBackfill_SkipsReportsCreatedAfterTheRange()
    {
        AddSchedule(AmazonReportType.Settlements);
        _gateway.Available.Add(new AvailableAmazonReport("IN", "D-IN", Now.AddDays(-20)));
        _gateway.Available.Add(new AvailableAmazonReport("AFTER", "D-AFTER", Now.AddDays(-2)));
        _gateway.Documents["D-IN"] = SettlementTsv;
        _gateway.Documents["D-AFTER"] = SettlementTsv;

        await RunToEndAsync(SyncTrigger.Manual, "ops", new BackfillWindow(Now.AddDays(-25), Now.AddDays(-10)), CancellationToken.None);

        Assert.Contains(_runs.Ledger, r => r.AmazonReportId == "IN");
        Assert.DoesNotContain(_runs.Ledger, r => r.AmazonReportId == "AFTER");
    }

    [Fact]
    public async Task RunAsync_AutoPromoteOff_StagesWithoutPromoting()
    {
        AddSchedule(AmazonReportType.Orders, autoPromote: false);
        ScriptDoneReport(OrdersTsv);

        var summary = await RunToEndAsync(SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Succeeded, summary.Status);
        Assert.Single(_staged.Saved);
        Assert.Empty(_promotion.Promoted);
        Assert.Contains("awaiting promotion", summary.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ReportCancelled_IsNoDataAndAdvancesWindow()
    {
        AddSchedule(AmazonReportType.Orders);
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.Cancelled, null));

        var summary = await RunToEndAsync(SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.NoData, summary.Status);
        Assert.Empty(_rawFiles.Files);
        Assert.NotNull(_schedules.Schedules[1].LastSuccessfulDataEnd);
    }

    [Fact]
    public async Task RunAsync_HeaderOnlyDocument_IsNoData()
    {
        AddSchedule(AmazonReportType.Orders);
        ScriptDoneReport("amazon-order-id\tpurchase-date\torder-status\tsku\tquantity\titem-price\n");

        var summary = await RunToEndAsync(SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.NoData, summary.Status);
        Assert.Single(_rawFiles.Files); // Still landed: the landing zone keeps everything received.
    }

    [Fact]
    public async Task RunAsync_ReportFatal_FailsAndDoesNotAdvanceWindow()
    {
        AddSchedule(AmazonReportType.Orders);
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.Fatal, null));

        var summary = await RunToEndAsync(SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Failed, summary.Status);
        Assert.Null(_schedules.Schedules[1].LastSuccessfulDataEnd);
    }

    [Fact]
    public async Task RunAsync_GatewayThrows_RecordsFailedRunInsteadOfThrowing()
    {
        AddSchedule(AmazonReportType.Orders);
        _gateway.ThrowOnRequest = new HttpRequestException("SP-API reports.createReport failed with HTTP 403");

        var summary = await RunToEndAsync(SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Failed, summary.Status);
        var run = Assert.Single(_runs.Completed);
        Assert.Contains("HTTP 403", run.Message, StringComparison.Ordinal);
        Assert.NotNull(run.CompletedAt);
    }

    [Fact]
    public async Task RunAsync_Settlements_IngestsOnlyReportsNotInLedger()
    {
        AddSchedule(AmazonReportType.Settlements);
        _gateway.Available.Add(new AvailableAmazonReport("OLD", "D-OLD", Now.AddDays(-3)));
        _gateway.Available.Add(new AvailableAmazonReport("NEW", "D-NEW", Now.AddDays(-1)));
        _gateway.Documents["D-NEW"] = SettlementTsv;
        _runs.Ledger.Add(new IngestedReport { AmazonReportId = "OLD", ReportType = AmazonReportType.Settlements });

        var summary = await RunToEndAsync(SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Succeeded, summary.Status);
        Assert.Equal(ImportSource.Settlements, Assert.Single(_staged.Saved).Source);
        Assert.Contains(_runs.Ledger, r => r.AmazonReportId == "NEW");
        Assert.Empty(_gateway.Requests); // Settlements are listed, never requested.
    }

    [Fact]
    public async Task RunAsync_SettlementsNothingNew_IsNoData()
    {
        AddSchedule(AmazonReportType.Settlements);

        var summary = await RunToEndAsync(SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.NoData, summary.Status);
    }

    private static IngestionOptions Polling => new()
    {
        ReportPollInterval = TimeSpan.FromSeconds(30),
        ReportPollMaxInterval = TimeSpan.FromMinutes(5),
        ReportMaxWait = TimeSpan.FromMinutes(45),
    };

    [Fact]
    public async Task RunAsync_RequestedReport_ReturnsWaitingWithFirstCheckScheduled()
    {
        AddSchedule(AmazonReportType.Orders);

        var summary = await CreateService(Polling).RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Running, summary.Status);
        var stored = _runs.Stored[summary.RunId];
        Assert.Equal("R1", stored.PendingReportId);
        Assert.Equal(Now.AddSeconds(30), stored.NextCheckAt);
        Assert.Empty(_runs.Completed);
        Assert.Empty(_rawFiles.Files);
    }

    [Fact]
    public async Task ContinueAsync_StillGenerating_DoublesTheDelayUpToTheCap()
    {
        AddSchedule(AmazonReportType.FbaInventory);
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.InProgress, null));
        var service = CreateService(Polling);
        var runId = (await service.RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None)).RunId;

        var delays = new List<TimeSpan>();
        for (var i = 0; i < 5; i++)
        {
            _clock.SetUtcNow(_runs.Stored[runId].NextCheckAt!.Value);
            Assert.Equal(SyncRunStatus.Running, (await service.ContinueAsync(runId, CancellationToken.None)).Status);
            delays.Add(_runs.Stored[runId].NextCheckAt!.Value - _clock.GetUtcNow());
        }

        Assert.Equal([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)], delays);
    }

    [Fact]
    public async Task ContinueAsync_PastMaxWait_FailsTheRun()
    {
        AddSchedule(AmazonReportType.FbaInventory);
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.InQueue, null));
        var service = CreateService(Polling);
        var runId = (await service.RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None)).RunId;

        _clock.Advance(TimeSpan.FromMinutes(46));
        var summary = await service.ContinueAsync(runId, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Failed, summary.Status);
        Assert.Contains("not ready after 45 minutes", summary.Message, StringComparison.Ordinal);
        Assert.Null(_runs.Stored[runId].PendingReportId);
    }

    [Fact]
    public async Task ContinueAsync_AfterRestart_FinishesTheWaitingRun()
    {
        AddSchedule(AmazonReportType.Orders);
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.Done, "D1"));
        _gateway.Documents["D1"] = OrdersTsv;
        var runId = (await CreateService(Polling).RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None)).RunId;

        // A fresh service instance has nothing in memory: everything comes from the saved run.
        var summary = await CreateService(Polling).ContinueAsync(runId, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Succeeded, summary.Status);
        Assert.Equal("R1", Assert.Single(_runs.Ledger).AmazonReportId);
        Assert.Equal("R1", _runs.Stored[runId].AmazonReportIds);
        Assert.Equal(_runs.Stored[runId].DataEnd, _schedules.Schedules[1].LastSuccessfulDataEnd);
    }

    [Fact]
    public async Task ContinueAsync_ReportAlreadyInLedger_DoesNotStageItAgain()
    {
        AddSchedule(AmazonReportType.Orders);
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.Done, "D1"));
        _gateway.Documents["D1"] = OrdersTsv;
        var service = CreateService(Polling);
        var runId = (await service.RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None)).RunId;
        _runs.Ledger.Add(new IngestedReport { AmazonReportId = "R1", ReportType = AmazonReportType.Orders });

        var summary = await service.ContinueAsync(runId, CancellationToken.None);

        Assert.Equal(SyncRunStatus.NoData, summary.Status);
        Assert.Empty(_staged.Saved);
        Assert.Contains("already ingested", summary.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContinueAsync_ShutdownMidCheck_LeavesTheRunWaitingAndDueNow()
    {
        AddSchedule(AmazonReportType.FbaInventory);
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.InProgress, null));
        var service = CreateService(Polling);
        var runId = (await service.RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None)).RunId;
        _clock.Advance(TimeSpan.FromMinutes(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ContinueAsync(runId, new CancellationToken(canceled: true)));

        var stored = _runs.Stored[runId];
        Assert.Equal(SyncRunStatus.Running, stored.Status);
        Assert.Equal("R1", stored.PendingReportId);
        Assert.Equal(_clock.GetUtcNow(), stored.NextCheckAt);
    }

    [Fact]
    public async Task ContinueAsync_FinishedRun_ReturnsItsOutcomeWithoutCallingAmazon()
    {
        AddSchedule(AmazonReportType.Settlements);
        var finished = await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);
        var seen = _gateway.MarketplacesSeen.Count;

        var summary = await CreateService().ContinueAsync(finished.RunId, CancellationToken.None);

        Assert.Equal(finished.Status, summary.Status);
        Assert.Equal(seen, _gateway.MarketplacesSeen.Count);
    }
}
