using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Domain.Reporting;
using AERai.Web.Domain.Staging;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// SQL-backed tests for scheduled ingestion: FBA inventory promotion, atomic schedule claiming, and
/// a full run through the simulated SP-API (gateway → blob → staging → promotion → views).
/// </summary>
public sealed class IngestionTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private async Task<int> AddScheduleAsync(AmazonReportType type, string name, int lookbackDays = 2, string marketplaceId = MarketplaceIds.UnitedStates)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISyncScheduleService>().CreateAsync(
            new SyncScheduleInput(name, type, marketplaceId, IsEnabled: true, ScheduleFrequency.Interval, 60, null, "UTC", lookbackDays, AutoPromote: true),
            "tests@aeraigroup.com", CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error);
        return result.Value;
    }

    private const string MyiHeader =
        "sku\tasin\tproduct-name\tafn-fulfillable-quantity\tafn-unsellable-quantity\tafn-reserved-quantity\tafn-inbound-working-quantity\tafn-inbound-shipped-quantity\tafn-inbound-receiving-quantity\n";

    private const string ReservedHeader =
        "sku\tfnsku\tasin\tproduct-name\treserved_qty\treserved_customerorders\treserved_fc-transfers\treserved_fc-processing\n";

    private async Task<PromotionSummary> StageAndPromoteAsync(ImportSource source, string tsv)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var bytes = Encoding.UTF8.GetBytes(tsv);
        var staged = await scope.ServiceProvider.GetRequiredService<IStagingImportService>()
            .ImportAsync(new ImportFileCommand(source, MarketplaceIds.UnitedStates, $"{source}.tsv", bytes.Length, new MemoryStream(bytes), "tests"), CancellationToken.None);
        Assert.True(staged.IsSuccess, staged.Error);

        var promoted = await scope.ServiceProvider.GetRequiredService<IPromotionService>().PromoteAsync(staged.Value.BatchId, CancellationToken.None);
        Assert.True(promoted.IsSuccess, promoted.Error);
        return promoted.Value;
    }

    private async Task<InventoryPosition?> PositionAsync(string sku)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().InventoryPositions.SingleOrDefaultAsync(p => p.Sku == sku);
    }

    [SqlFact]
    public async Task FbaInventory_UnpivotsWideRowsIntoStateSnapshots()
    {
        var promoted = await StageAndPromoteAsync(ImportSource.FbaInventory,
            MyiHeader +
            "T-FBA-1\tB0TEST0001\tFba Widget\t40\t2\t5\t10\t20\t3\n" +
            "T-FBA-2\tB0TEST0002\tFba Gadget\tlots\t0\t0\t0\t0\t0\n");

        Assert.Equal((1, 1), (promoted.PromotedRowCount, promoted.RejectedRowCount));
        var position = await PositionAsync("T-FBA-1");
        Assert.NotNull(position);
        Assert.Equal((40, 5, 2), (position.Available, position.ReservedUnsplit, position.Unfulfillable));
        Assert.Equal((10, 20, 3, 0), (position.InboundWorking, position.InboundShipped, position.InboundReceiving, position.InboundUnsplit));
        Assert.Equal("B0TEST0001", position.Asin);
    }

    [SqlFact]
    public async Task FbaReserved_AfterMainReport_ReplacesReservedTotalWithBreakdown()
    {
        await StageAndPromoteAsync(ImportSource.FbaInventory, MyiHeader + "T-RES-1\tB0RES00001\tWidget\t40\t0\t9\t0\t0\t0\n");
        await StageAndPromoteAsync(ImportSource.FbaReservedInventory, ReservedHeader + "T-RES-1\tX001\tB0RES00001\tWidget\t9\t4\t3\t2\n");

        var position = await PositionAsync("T-RES-1");

        Assert.NotNull(position);
        Assert.True(position.HasReservedBreakdown);
        Assert.Equal((4, 3, 2, 9), (position.ReservedCustomerOrder, position.ReservedFcTransfer, position.ReservedFcProcessing, position.Reserved));
        Assert.Equal(40, position.Available);
    }

    [SqlFact]
    public async Task FbaReserved_BeforeMainReport_BreakdownSurvivesMainReport()
    {
        await StageAndPromoteAsync(ImportSource.FbaReservedInventory, ReservedHeader + "T-RES-2\tX002\tB0RES00002\tGadget\t6\t1\t5\t0\n");

        // Reserved reasons alone are not a full snapshot, so the SKU has no position yet.
        Assert.Null(await PositionAsync("T-RES-2"));

        await StageAndPromoteAsync(ImportSource.FbaInventory, MyiHeader + "T-RES-2\tB0RES00002\tGadget\t12\t0\t6\t0\t0\t0\n");
        var position = await PositionAsync("T-RES-2");

        Assert.NotNull(position);
        Assert.Equal((12, 1, 5, 0, 0), (position.Available, position.ReservedCustomerOrder, position.ReservedFcTransfer, position.ReservedFcProcessing, position.ReservedUnsplit));
    }

    [SqlFact]
    public async Task TryClaim_TwoInstancesRacingForOneSlot_OnlyOneWins()
    {
        var id = await AddScheduleAsync(AmazonReportType.Orders, "claim-race");

        await using var scope1 = fixture.Services.CreateAsyncScope();
        await using var scope2 = fixture.Services.CreateAsyncScope();
        var repo1 = scope1.ServiceProvider.GetRequiredService<ISyncScheduleRepository>();
        var repo2 = scope2.ServiceProvider.GetRequiredService<ISyncScheduleRepository>();

        var slot = (await repo1.GetAsync(id, CancellationToken.None))!.NextRunAt!.Value;
        var now = DateTimeOffset.UtcNow;

        var claims = await Task.WhenAll(
            repo1.TryClaimAsync(id, slot, slot.AddHours(1), now, CancellationToken.None),
            repo2.TryClaimAsync(id, slot, slot.AddHours(1), now, CancellationToken.None));

        Assert.Single(claims, won => won);
    }

    [SqlFact]
    public async Task RecordSuccess_OlderWindow_NeverMovesMarkerBackwards()
    {
        var id = await AddScheduleAsync(AmazonReportType.Orders, "marker-forward-only");
        await using var scope = fixture.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISyncScheduleRepository>();
        var later = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

        await repo.RecordSuccessAsync(id, later, CancellationToken.None);
        await repo.RecordSuccessAsync(id, later.AddDays(-10), CancellationToken.None);

        Assert.Equal(later, (await repo.GetAsync(id, CancellationToken.None))!.LastSuccessfulDataEnd);
    }

    [SqlFact]
    public async Task SimulatedOrdersRun_FlowsFromGatewayThroughBlobStagingAndPromotionIntoViews()
    {
        var id = await AddScheduleAsync(AmazonReportType.Orders, "sim-orders");

        await using var scope = fixture.Services.CreateAsyncScope();
        var summary = await scope.ServiceProvider.GetRequiredService<IReportIngestionService>()
            .RunAsync(id, SyncTrigger.Manual, "tests@aeraigroup.com", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Succeeded, summary.Status);
        var batchId = Assert.Single(summary.ImportBatchIds);

        var batch = await scope.ServiceProvider.GetRequiredService<IImportBatchQueries>().GetAsync(batchId, CancellationToken.None);
        Assert.Equal(ImportBatchStatus.Promoted, batch!.Status);
        Assert.StartsWith("orders/", batch.RawFilePath, StringComparison.Ordinal);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.OrderSummaries.AnyAsync());
        Assert.True(await db.IngestedReports.AnyAsync(r => r.ImportBatchId == batchId));
        Assert.NotNull((await db.SyncSchedules.SingleAsync(s => s.Id == id)).LastSuccessfulDataEnd);
        Assert.Equal(SyncRunStatus.Succeeded, (await db.SyncRuns.SingleAsync(r => r.Id == summary.RunId)).Status);
    }

    [SqlFact]
    public async Task SimulatedSettlementsRun_SecondRunIngestsNothingNew()
    {
        // 30 days always spans at least one of the simulator's 14-day settlement periods.
        var id = await AddScheduleAsync(AmazonReportType.Settlements, "sim-settlements", lookbackDays: 30);

        await using var scope = fixture.Services.CreateAsyncScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<IReportIngestionService>();

        var first = await ingestion.RunAsync(id, SyncTrigger.Manual, "tests", backfill: null, CancellationToken.None);
        var second = await ingestion.RunAsync(id, SyncTrigger.Manual, "tests", backfill: null, CancellationToken.None);

        Assert.Equal(SyncRunStatus.Succeeded, first.Status);
        Assert.Equal(SyncRunStatus.NoData, second.Status);
    }
}
