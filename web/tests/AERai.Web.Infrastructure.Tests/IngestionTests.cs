using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Ingestion;
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
    private async Task<int> AddScheduleAsync(AmazonReportType type, string name, int lookbackDays = 2)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISyncScheduleService>().CreateAsync(
            new SyncScheduleInput(name, type, IsEnabled: true, ScheduleFrequency.Interval, 60, null, "UTC", lookbackDays, AutoPromote: true),
            "tests@aeraigroup.com", CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error);
        return result.Value;
    }

    [SqlFact]
    public async Task FbaInventory_UnpivotsWideRowsIntoStateSnapshots()
    {
        const string tsv =
            "sku\tasin\tproduct-name\tafn-fulfillable-quantity\tafn-unsellable-quantity\tafn-reserved-quantity\tafn-inbound-working-quantity\tafn-inbound-shipped-quantity\tafn-inbound-receiving-quantity\n" +
            "T-FBA-1\tB0TEST0001\tFba Widget\t40\t2\t5\t10\t20\t3\n" +
            "T-FBA-2\tB0TEST0002\tFba Gadget\tlots\t0\t0\t0\t0\t0\n";

        await using var scope = fixture.Services.CreateAsyncScope();
        var bytes = Encoding.UTF8.GetBytes(tsv);
        var staged = await scope.ServiceProvider.GetRequiredService<IStagingImportService>()
            .ImportAsync(new ImportFileCommand(ImportSource.FbaInventory, "myi.tsv", bytes.Length, new MemoryStream(bytes), "tests"), CancellationToken.None);
        Assert.True(staged.IsSuccess, staged.Error);

        var promoted = await scope.ServiceProvider.GetRequiredService<IPromotionService>().PromoteAsync(staged.Value.BatchId, CancellationToken.None);

        Assert.Equal((1, 1), (promoted.Value.PromotedRowCount, promoted.Value.RejectedRowCount));
        var position = await scope.ServiceProvider.GetRequiredService<AppDbContext>().InventoryPositions.SingleAsync(p => p.Sku == "T-FBA-1");
        Assert.Equal((40, 33, 5, 2), (position.Available, position.Inbound, position.Reserved, position.Unfulfillable));
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
