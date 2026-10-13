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
    private readonly FakeAmazonAwdGateway _awd = new();
    private readonly FakeAwdInventoryRepository _awdInventory = new();
    private readonly FakeAmazonOrdersGateway _ordersApi = new();
    private readonly FakeAmazonOrderRepository _apiOrders = new();
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

        return new ReportIngestionService(_gateway, _awd, _awdInventory, _ordersApi, _apiOrders, _rawFiles, staging, _promotion, _schedules, _runs, _marketplaces, options, _clock,
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
    public async Task RunAsync_AwdInventory_PagesThroughLandsEachPageAndReplacesTheDaysSnapshot()
    {
        AddSchedule(AmazonReportType.AwdInventory);
        _awd.Pages.Add([new AwdInventoryItem("A-1", 100, 20, 90, 10, 5), new AwdInventoryItem("A-2", 0, 0, 0, 0, 0)]);
        _awd.Pages.Add([new AwdInventoryItem("A-3", 40, 0, 40, 0, 0)]);

        var summary = await CreateService().RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Succeeded, summary.Status);
        Assert.Equal([null, "1"], _awd.TokensSeen);
        Assert.Equal(2, _rawFiles.Files.Keys.Count(k => k.StartsWith("awdinventory/2026/10/02/", StringComparison.Ordinal)));
        var snapshot = Assert.Single(_awdInventory.Snapshots);
        Assert.Equal((MarketplaceIds.UnitedStates, new DateOnly(2026, 10, 2)), (snapshot.MarketplaceId, snapshot.SnapshotDate));

        // A SKU listed with nothing in AWD is not stored.
        Assert.Equal(["A-1", "A-3"], snapshot.Items.Select(i => i.Sku).Order(StringComparer.Ordinal));
        Assert.Empty(_staged.Saved);
        Assert.Empty(_promotion.Promoted);
        Assert.Empty(_gateway.Requests);
        Assert.Contains("2 SKUs", summary.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_AwdInventoryEmpty_RecordsNoDataAndClearsTheDaysSnapshot()
    {
        AddSchedule(AmazonReportType.AwdInventory);

        var summary = await CreateService().RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.NoData, summary.Status);
        Assert.Empty(Assert.Single(_awdInventory.Snapshots).Items);
        Assert.NotNull(_schedules.Schedules[1].LastSuccessfulDataEnd);
    }

    [Fact]
    public async Task RunAsync_AwdListingNeverEnds_FailsWithoutWriting()
    {
        AddSchedule(AmazonReportType.AwdInventory);
        _awd.Pages.Add([new AwdInventoryItem("A-1", 1, 0, 1, 0, 0)]);
        _awd.NeverEnds = true;

        var summary = await CreateService().RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Failed, summary.Status);
        Assert.Contains("did not end", summary.Message, StringComparison.Ordinal);
        Assert.Empty(_awdInventory.Snapshots);
    }

    [Fact]
    public async Task RunAsync_AwdGatewayFails_FailsWithoutWriting()
    {
        AddSchedule(AmazonReportType.AwdInventory);
        _awd.Failure = new HttpRequestException("SP-API awd.listInventory failed with HTTP 403");

        var summary = await CreateService().RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Failed, summary.Status);
        Assert.Contains("403", summary.Message, StringComparison.Ordinal);
        Assert.Empty(_awdInventory.Snapshots);
    }

    private static AmazonOrder ApiOrder(string id, DateTimeOffset? updated = null) =>
        new(id, Now.AddHours(-5), updated ?? Now.AddHours(-1), "Shipped", "USD", [new AmazonOrderLine("A-1", "B000000001", "Widget", 2, 19.98m)]);

    [Fact]
    public async Task RunAsync_OrdersApi_PagesThroughLandsEachPageAndUpsertsAsItGoes()
    {
        AddSchedule(AmazonReportType.OrdersApi);
        _ordersApi.Pages.Add([ApiOrder("111-1"), ApiOrder("111-2")]);
        _ordersApi.Pages.Add([ApiOrder("111-3")]);

        var summary = await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Succeeded, summary.Status);
        Assert.Equal([null, "1"], _ordersApi.TokensSeen);
        Assert.Equal(2, _rawFiles.Files.Keys.Count(k => k.StartsWith("ordersapi/2026/10/02/", StringComparison.Ordinal)));
        Assert.Equal([2, 1], _apiOrders.Writes.Select(w => w.Orders.Count));
        Assert.All(_apiOrders.Writes, w => Assert.Equal(MarketplaceIds.UnitedStates, w.MarketplaceId));
        Assert.Empty(_staged.Saved);
        Assert.Empty(_promotion.Promoted);
        Assert.Empty(_gateway.Requests);
        Assert.Contains("3 orders (3 new, 0 updated)", summary.Message, StringComparison.Ordinal);
        Assert.NotNull(_schedules.Schedules[1].LastSuccessfulDataEnd);
    }

    [Fact]
    public async Task RunAsync_OrdersApiFirstRun_AsksForOrdersUpdatedWithinTheLookback()
    {
        AddSchedule(AmazonReportType.OrdersApi);
        _ordersApi.Pages.Add([ApiOrder("111-1")]);

        await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        var filter = Assert.Single(_ordersApi.FiltersSeen);
        var end = Now - ReportIngestionService.DataEndSafetyMargin;
        Assert.Equal(end.AddDays(-7), filter.LastUpdatedAfter);
        Assert.Null(filter.CreatedAfter);
        Assert.Null(filter.CreatedBefore);
    }

    [Fact]
    public async Task RunAsync_OrdersApiLaterRun_ResumesFromThePreviousEndWithOverlap()
    {
        var previousEnd = Now.AddHours(-4);
        AddSchedule(AmazonReportType.OrdersApi, lastEnd: previousEnd);
        _ordersApi.Pages.Add([ApiOrder("111-1")]);

        await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(previousEnd - TimeSpan.FromMinutes(15), Assert.Single(_ordersApi.FiltersSeen).LastUpdatedAfter);
    }

    [Fact]
    public async Task RunAsync_OrdersApiBackfill_AsksForOrdersCreatedInTheRange()
    {
        AddSchedule(AmazonReportType.OrdersApi);
        _ordersApi.Pages.Add([ApiOrder("111-1")]);
        var window = new BackfillWindow(Now.AddDays(-30), Now.AddDays(-20));

        await CreateService().RunAsync(1, SyncTrigger.Manual, "ops", window, CancellationToken.None);

        var filter = Assert.Single(_ordersApi.FiltersSeen);
        Assert.Equal(window.Start, filter.CreatedAfter);
        Assert.Equal(window.End, filter.CreatedBefore);
        Assert.Null(filter.LastUpdatedAfter);
    }

    [Fact]
    public async Task RunAsync_OrdersApiBackfillEndingNow_StopsSafelyBeforeNowSoAmazonAcceptsIt()
    {
        AddSchedule(AmazonReportType.OrdersApi);
        _ordersApi.Pages.Add([ApiOrder("111-1")]);

        await CreateService().RunAsync(1, SyncTrigger.Manual, "ops", new BackfillWindow(Now.AddDays(-283), Now), CancellationToken.None);

        // Amazon rejects createdBefore less than 2 minutes before its own clock; ours may run a little ahead.
        var filter = Assert.Single(_ordersApi.FiltersSeen);
        Assert.Equal(Now - ReportIngestionService.DataEndSafetyMargin, filter.CreatedBefore);
        Assert.True(ReportIngestionService.DataEndSafetyMargin > TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task RunAsync_OrdersApiRepeatedOrderOnAPage_WritesItOnce()
    {
        AddSchedule(AmazonReportType.OrdersApi);
        _ordersApi.Pages.Add([ApiOrder("111-1", Now.AddHours(-3)), ApiOrder("111-1", Now.AddHours(-1))]);

        await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        var written = Assert.Single(Assert.Single(_apiOrders.Writes).Orders);
        Assert.Equal(Now.AddHours(-1), written.LastUpdatedAt);
    }

    [Fact]
    public async Task RunAsync_OrdersApiNoOrders_RecordsNoData()
    {
        AddSchedule(AmazonReportType.OrdersApi);

        var summary = await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.NoData, summary.Status);
        Assert.Empty(_apiOrders.Writes);
        Assert.NotNull(_schedules.Schedules[1].LastSuccessfulDataEnd);
    }

    [Fact]
    public async Task RunAsync_OrdersApiSearchNeverEnds_Fails()
    {
        AddSchedule(AmazonReportType.OrdersApi);
        _ordersApi.Pages.Add([ApiOrder("111-1")]);
        _ordersApi.NeverEnds = true;

        var summary = await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Failed, summary.Status);
        Assert.Contains("did not end", summary.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_OrdersApiGatewayFails_FailsWithoutMovingTheScheduleForward()
    {
        AddSchedule(AmazonReportType.OrdersApi);
        _ordersApi.Failure = new HttpRequestException("SP-API orders.searchOrders failed with HTTP 403");

        var summary = await CreateService().RunAsync(1, SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Failed, summary.Status);
        Assert.Contains("403", summary.Message, StringComparison.Ordinal);
        Assert.Null(_schedules.Schedules[1].LastSuccessfulDataEnd);
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
        Assert.Equal((Now - ReportIngestionService.DataEndSafetyMargin).AddDays(-7), request.Start);
        Assert.Equal(Now - ReportIngestionService.DataEndSafetyMargin, request.End);

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

        var summary = await RunToEndAsync(SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Succeeded, summary.Status);
        Assert.Contains("1 promoted, 0 rejected, 2,644 for other marketplaces.", summary.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_NoRowsSkipped_LeavesOtherMarketplacesOutOfTheNote()
    {
        AddSchedule(AmazonReportType.Orders);
        ScriptDoneReport(OrdersTsv);

        var summary = await RunToEndAsync(SyncTrigger.Scheduled, "scheduler", backfill: null, CancellationToken.None);

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
    public async Task RunAsync_SettlementsBackfillOlderThanAmazonLists_StartsAtTheLimitAndSaysSo()
    {
        AddSchedule(AmazonReportType.Settlements);
        _gateway.Available.Add(new AvailableAmazonReport("RECENT", "D-RECENT", Now.AddDays(-20)));
        _gateway.Documents["D-RECENT"] = SettlementTsv;

        var summary = await RunToEndAsync(SyncTrigger.Manual, "ops", new BackfillWindow(Now.AddDays(-250), Now), CancellationToken.None);

        // Amazon rejects a createdSince more than 90 days old with a 400, so the request starts inside the limit.
        Assert.Equal(SyncRunStatus.Succeeded, summary.Status);
        Assert.Equal(Now - ReportIngestionService.MaxListingAge, Assert.Single(_gateway.SinceSeen));
        Assert.Contains("only lists reports from the last 90 days", summary.Message, StringComparison.Ordinal);
        Assert.Contains(_runs.Ledger, r => r.AmazonReportId == "RECENT");
    }

    [Fact]
    public async Task RunAsync_SettlementsStartInsideTheLimit_IsNotChangedAndHasNoNote()
    {
        AddSchedule(AmazonReportType.Settlements);

        var summary = await RunToEndAsync(SyncTrigger.Manual, "ops", BackfillWindow.LastDays(30, Now), CancellationToken.None);

        Assert.Equal(Now.AddDays(-30), Assert.Single(_gateway.SinceSeen));
        Assert.DoesNotContain("90 days", summary.Message, StringComparison.Ordinal);
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
    public async Task RunAsync_ReportFatalWithErrorDocument_RecordsAmazonsReason()
    {
        AddSchedule(AmazonReportType.FbaInventory);
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.Fatal, "ERR1"));
        _gateway.Documents["ERR1"] = """{"errorDetails":"Report was requested too often."}""";

        var summary = await RunToEndAsync(SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Failed, summary.Status);
        Assert.Equal("Amazon could not generate report R1 (status Fatal): Report was requested too often.", summary.Message);
        Assert.Empty(_rawFiles.Files);
    }

    [Fact]
    public async Task RunAsync_ReportFatalErrorDocumentUnreadable_FailsWithThePlainMessage()
    {
        AddSchedule(AmazonReportType.FbaInventory);
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.Fatal, "MISSING"));

        var summary = await RunToEndAsync(SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Failed, summary.Status);
        Assert.Equal("Amazon could not generate report R1 (status Fatal).", summary.Message);
    }

    [Fact]
    public async Task RunAsync_FbaInventoryInsideCooldown_WaitsAndRequestsWhenItEnds()
    {
        AddSchedule(AmazonReportType.FbaInventory);
        ScriptDoneReport("sku\tafn-fulfillable-quantity\n");
        var service = CreateService(Polling);
        var first = await service.RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(10));

        var second = await service.RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        // Held back until 30 minutes (plus a minute's margin) after the first request, without asking Amazon.
        Assert.Equal(SyncRunStatus.Running, second.Status);
        var waiting = _runs.Stored[second.RunId];
        Assert.Null(waiting.PendingReportId);
        Assert.Equal(Now.AddMinutes(31), waiting.NextCheckAt);
        Assert.Single(_gateway.Requests);

        _clock.SetUtcNow(waiting.NextCheckAt!.Value);
        var continued = await service.ContinueAsync(second.RunId, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Running, continued.Status);
        Assert.Equal(2, _gateway.Requests.Count);
        Assert.Equal("R2", _runs.Stored[second.RunId].PendingReportId);
        Assert.Equal(Now.AddMinutes(31), _runs.Stored[second.RunId].ReportRequestedAt);
        Assert.Equal(SyncRunStatus.Running, _runs.Stored[first.RunId].Status);
    }

    [Fact]
    public async Task RunAsync_FbaInventoryInsideCooldownWithARunAlreadyWaiting_EndsWithoutRequesting()
    {
        AddSchedule(AmazonReportType.FbaInventory);
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.InProgress, null));
        var service = CreateService(Polling);
        await service.RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);
        var held = await service.RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        var third = await service.RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Running, held.Status);
        Assert.Equal(SyncRunStatus.NoData, third.Status);
        Assert.Contains("Not requested", third.Message, StringComparison.Ordinal);
        Assert.Single(_gateway.Requests);
        Assert.Null(_schedules.Schedules[1].LastSuccessfulDataEnd);
    }

    [Fact]
    public async Task RunAsync_FbaInventoryAfterCooldown_RequestsStraightAway()
    {
        AddSchedule(AmazonReportType.FbaInventory);
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.InProgress, null));
        var service = CreateService(Polling);
        await service.RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(31));

        var summary = await service.RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        Assert.Equal("R2", _runs.Stored[summary.RunId].PendingReportId);
    }

    [Fact]
    public async Task RunAsync_RestockSoonAfterAnotherRequest_RequestsStraightAway()
    {
        AddSchedule(AmazonReportType.RestockRecommendations);
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.InProgress, null));
        var service = CreateService(Polling);
        await service.RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        var summary = await service.RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);

        Assert.Equal("R2", _runs.Stored[summary.RunId].PendingReportId);
    }

    [Fact]
    public async Task ContinueAsync_MaxWaitCountsFromTheRequestNotTheRunsStart()
    {
        AddSchedule(AmazonReportType.FbaInventory);
        _gateway.Statuses.Enqueue(new AmazonReportStatus(AmazonProcessingStatus.InProgress, null));
        var service = CreateService(Polling);
        await service.RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);
        var held = await service.RunAsync(1, SyncTrigger.Manual, "ops", backfill: null, CancellationToken.None);
        _clock.SetUtcNow(_runs.Stored[held.RunId].NextCheckAt!.Value);
        await service.ContinueAsync(held.RunId, CancellationToken.None);

        // 50 minutes after the run started, but only 19 after its report was requested.
        _clock.SetUtcNow(Now.AddMinutes(50));
        var summary = await service.ContinueAsync(held.RunId, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Running, summary.Status);
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
