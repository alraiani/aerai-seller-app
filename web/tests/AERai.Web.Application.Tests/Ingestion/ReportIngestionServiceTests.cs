using AERai.Web.Application.Imports;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Tests.Fakes;
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
    private readonly FakeTimeProvider _clock = new(Now);

    private ReportIngestionService CreateService()
    {
        var staging = new StagingImportService(
            [new OrderLineMapper(), new SettlementLineMapper(), new FbaInventoryRowMapper()],
            _rawFiles, _staged, new FakeImportBatchQueries(), Options.Create(new ImportOptions()), _clock,
            NullLogger<StagingImportService>.Instance);

        // Zero poll interval: Task.Delay completes immediately, so polling loops run without waiting.
        var options = Options.Create(new IngestionOptions { ReportPollInterval = TimeSpan.Zero, ReportMaxWait = TimeSpan.FromMinutes(5) });

        return new ReportIngestionService(_gateway, _rawFiles, staging, _promotion, _schedules, _runs, options, _clock,
            NullLogger<ReportIngestionService>.Instance);
    }

    private SyncSchedule AddSchedule(AmazonReportType type, bool autoPromote = true, DateTimeOffset? lastEnd = null)
    {
        var schedule = new SyncSchedule
        {
            Id = 1, Name = "s", ReportType = type, IsEnabled = true, Frequency = ScheduleFrequency.Interval, IntervalMinutes = 60,
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
    public async Task RunAsync_FirstOrdersRun_RequestsLookbackWindowAndLandsStagesAndPromotes()
    {
        AddSchedule(AmazonReportType.Orders);
        ScriptDoneReport(OrdersTsv);

        var summary = await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfillDays: null, CancellationToken.None);

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
    public async Task RunAsync_LaterOrdersRun_ContinuesFromLastEndWithOverlap()
    {
        var lastEnd = Now.AddHours(-1);
        AddSchedule(AmazonReportType.Orders, lastEnd: lastEnd);
        ScriptDoneReport(OrdersTsv);

        await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfillDays: null, CancellationToken.None);

        Assert.Equal(lastEnd.AddMinutes(-15), Assert.Single(_gateway.Requests).Start);
    }

    [Fact]
    public async Task RunAsync_OrdersBackfill_RequestsFullWindowInsteadOfContinuing()
    {
        AddSchedule(AmazonReportType.Orders, lastEnd: Now.AddHours(-1));
        ScriptDoneReport(OrdersTsv);

        var summary = await CreateService().RunAsync(1, SyncTrigger.Manual, "ops", backfillDays: 30, CancellationToken.None);

        var request = Assert.Single(_gateway.Requests);
        Assert.Equal(request.End!.Value.AddDays(-30), request.Start);
        Assert.StartsWith("Backfill of 30 days.", summary.Message, StringComparison.Ordinal);
        Assert.Equal(request.End, _schedules.Schedules[1].LastSuccessfulDataEnd); // Moved forward, never back.
    }

    [Fact]
    public async Task RunAsync_SettlementsBackfill_ListsFromBackfillStart()
    {
        AddSchedule(AmazonReportType.Settlements, lastEnd: Now.AddHours(-1));
        _gateway.Available.Add(new AvailableAmazonReport("OLDER", "D-OLDER", Now.AddDays(-20)));
        _gateway.Documents["D-OLDER"] = SettlementTsv;

        var summary = await CreateService().RunAsync(1, SyncTrigger.Manual, "ops", backfillDays: 30, CancellationToken.None);

        // Without the backfill, listing would start an hour ago (minus overlap) and miss this report.
        Assert.Equal(SyncRunStatus.Succeeded, summary.Status);
        Assert.Contains(_runs.Ledger, r => r.AmazonReportId == "OLDER");
    }

    [Fact]
    public async Task RunAsync_AutoPromoteOff_StagesWithoutPromoting()
    {
        AddSchedule(AmazonReportType.Orders, autoPromote: false);
        ScriptDoneReport(OrdersTsv);

        var summary = await CreateService().RunAsync(1, SyncTrigger.Manual, "ops", backfillDays: null, CancellationToken.None);

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

        var summary = await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfillDays: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.NoData, summary.Status);
        Assert.Empty(_rawFiles.Files);
        Assert.NotNull(_schedules.Schedules[1].LastSuccessfulDataEnd);
    }

    [Fact]
    public async Task RunAsync_HeaderOnlyDocument_IsNoData()
    {
        AddSchedule(AmazonReportType.Orders);
        ScriptDoneReport("amazon-order-id\tpurchase-date\torder-status\tsku\tquantity\titem-price\n");

        var summary = await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfillDays: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.NoData, summary.Status);
        Assert.Single(_rawFiles.Files); // Still landed: the landing zone keeps everything received.
    }

    [Fact]
    public async Task RunAsync_ReportFatal_FailsAndDoesNotAdvanceWindow()
    {
        AddSchedule(AmazonReportType.Orders);
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.Fatal, null));

        var summary = await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfillDays: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Failed, summary.Status);
        Assert.Null(_schedules.Schedules[1].LastSuccessfulDataEnd);
    }

    [Fact]
    public async Task RunAsync_GatewayThrows_RecordsFailedRunInsteadOfThrowing()
    {
        AddSchedule(AmazonReportType.Orders);
        _gateway.ThrowOnRequest = new HttpRequestException("SP-API reports.createReport failed with HTTP 403");

        var summary = await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfillDays: null, CancellationToken.None);

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

        var summary = await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfillDays: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Succeeded, summary.Status);
        Assert.Equal(ImportSource.Settlements, Assert.Single(_staged.Saved).Source);
        Assert.Contains(_runs.Ledger, r => r.AmazonReportId == "NEW");
        Assert.Empty(_gateway.Requests); // Settlements are listed, never requested.
    }

    [Fact]
    public async Task RunAsync_SettlementsNothingNew_IsNoData()
    {
        AddSchedule(AmazonReportType.Settlements);

        var summary = await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfillDays: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.NoData, summary.Status);
    }
}
