using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using AERai.Web.Domain.Staging;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// End-to-end tests of the stg → core → rpt flow against a real SQL Server: staging via the
/// Application import service, promotion via the stored procedure, and reads via the views.
/// </summary>
public sealed class PromotionTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private async Task<long> StageAsync(ImportSource source, string fileName, string content)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var bytes = Encoding.UTF8.GetBytes(content);
        var result = await scope.ServiceProvider.GetRequiredService<IStagingImportService>()
            .ImportAsync(new ImportFileCommand(source, fileName, bytes.Length, new MemoryStream(bytes), "tests@aeraigroup.com"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        return result.Value.BatchId;
    }

    private async Task<PromotionSummary> PromoteAsync(long batchId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IPromotionService>().PromoteAsync(batchId, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        return result.Value;
    }

    [SqlFact]
    public async Task Orders_PromoteTwice_IsIdempotentAndRejectsBadRows()
    {
        const string csv =
            "amazon-order-id,purchase-date,order-status,sku,product-name,quantity,item-price,currency\n" +
            "T-ORD-1,2026-10-01T10:00:00Z,Shipped,T-SKU-A,Widget A,2,20.00,usd\n" +
            "T-ORD-1,2026-10-01T10:00:00Z,Shipped,T-SKU-B,Widget B,1,5.50,usd\n" +
            "T-ORD-2,2026-10-01T23:30:00-07:00,Shipped,T-SKU-A,Widget A,1,10.00,usd\n" +
            "T-ORD-3,not-a-date,Shipped,T-SKU-A,Widget A,1,10.00,usd\n";

        var batchId = await StageAsync(ImportSource.Orders, "orders.csv", csv);

        var first = await PromoteAsync(batchId);
        var second = await PromoteAsync(batchId);

        Assert.Equal(ImportBatchStatus.PromotedWithErrors, first.Status);
        Assert.Equal((3, 1), (first.PromotedRowCount, first.RejectedRowCount));
        Assert.Equal((3, 1), (second.PromotedRowCount, second.RejectedRowCount));

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(2, await db.Orders.CountAsync(o => o.AmazonOrderId.StartsWith("T-ORD-")));
        Assert.Equal(3, await db.OrderItems.CountAsync(i => i.Sku.StartsWith("T-SKU-")));
        Assert.Equal("USD", (await db.Orders.SingleAsync(o => o.AmazonOrderId == "T-ORD-1")).Currency);

        // T-ORD-2 was placed 23:30 at UTC-7, which is the next UTC day.
        var sales = await db.DailySalesBySku.Where(s => s.Sku == "T-SKU-A").OrderBy(s => s.SalesDate).ToListAsync();
        Assert.Equal([new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2)], sales.Select(s => s.SalesDate));
        Assert.Equal(20.00m, sales[0].Revenue);

        var rejected = await scope.ServiceProvider.GetRequiredService<IImportBatchQueries>()
            .GetRejectedRowsAsync(batchId, new PageRequest(), CancellationToken.None);
        var row = Assert.Single(rejected.Items);
        Assert.Equal(4, row.RowNumber);
        Assert.Contains("purchase-date", row.ErrorMessage, StringComparison.Ordinal);
    }

    [SqlFact]
    public async Task Inventory_RawStatesRollUpAndSnapshotReplacesPriorStates()
    {
        const string first =
            "snapshot-date\tsku\tstate\tquantity\n" +
            "2026-10-02\tT-INV-1\tAvailable\t10\n" +
            "2026-10-02\tT-INV-1\tinbound-working\t3\n" +
            "2026-10-02\tT-INV-1\tInbound-Shipped\t4\n" +
            "2026-10-02\tT-INV-1\tReserved\t2\n" +
            "2026-10-02\tT-INV-1\tTeleported\t1\n";

        // Same SKU and date re-reported without the Reserved state: Reserved must become zero.
        const string second =
            "snapshot-date\tsku\tstate\tquantity\n" +
            "2026-10-02\tT-INV-1\tAvailable\t8\n";

        var firstSummary = await PromoteAsync(await StageAsync(ImportSource.Inventory, "inv1.tsv", first));
        Assert.Equal(1, firstSummary.RejectedRowCount);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var position = await scope.ServiceProvider.GetRequiredService<AppDbContext>().InventoryPositions.SingleAsync(p => p.Sku == "T-INV-1");
            Assert.Equal((10, 7, 2), (position.Available, position.Inbound, position.Reserved));
        }

        await PromoteAsync(await StageAsync(ImportSource.Inventory, "inv2.tsv", second));

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var position = await scope.ServiceProvider.GetRequiredService<AppDbContext>().InventoryPositions.SingleAsync(p => p.Sku == "T-INV-1");
            Assert.Equal((8, 0, 0), (position.Available, position.Inbound, position.Reserved));
        }
    }

    [SqlFact]
    public async Task Settlements_HeaderRowSetsPeriodAndLinesAreBucketed()
    {
        const string tsv =
            "settlement-id\tsettlement-start-date\tsettlement-end-date\ttransaction-type\tamount-type\tamount-description\tamount\tcurrency\n" +
            "T-SET-1\t2026-09-04 07:00:00 UTC\t2026-09-18 07:00:00 UTC\t\t\t\t\tUSD\n" +
            "T-SET-1\t\t\tOrder\tItemPrice\tPrincipal\t100.00\t\n" +
            "T-SET-1\t\t\tOrder\tItemFees\tCommission\t-15.00\t\n" +
            "T-SET-1\t\t\tRefund\tItemPrice\tPrincipal\t-20.00\t\n" +
            "T-SET-1\t\t\tServiceFee\tother-transaction\tSubscription\t-39.99\t\n" +
            "T-SET-1\t\t\tOrder\tPromotion\tShipping\t-1.00\t\n";

        var batchId = await StageAsync(ImportSource.Settlements, "settlement.tsv", tsv);
        await PromoteAsync(batchId);
        await PromoteAsync(batchId);

        await using var scope = fixture.Services.CreateAsyncScope();
        var summary = await scope.ServiceProvider.GetRequiredService<AppDbContext>().SettlementSummaries.SingleAsync(s => s.SettlementId == "T-SET-1");

        Assert.Equal(new DateTimeOffset(2026, 9, 4, 7, 0, 0, TimeSpan.Zero), summary.PeriodStart);
        Assert.Equal((100.00m, -54.99m, -20.00m, -1.00m), (summary.Sales, summary.Fees, summary.Refunds, summary.Other));
        Assert.Equal(24.01m, summary.Net);
    }

    [SqlFact]
    public async Task Restage_FromRawFile_CreatesIndependentBatchThatPromotesIdentically()
    {
        const string csv =
            "amazon-order-id,purchase-date,order-status,sku,quantity,item-price\n" +
            "T-RST-1,2026-09-30T08:00:00Z,Shipped,T-RST-SKU,4,40.00\n";

        var originalId = await StageAsync(ImportSource.Orders, "restage.csv", csv);

        await using var scope = fixture.Services.CreateAsyncScope();
        var restaged = await scope.ServiceProvider.GetRequiredService<IStagingImportService>()
            .RestageAsync(originalId, "tests@aeraigroup.com", CancellationToken.None);
        Assert.True(restaged.IsSuccess, restaged.Error);
        Assert.NotEqual(originalId, restaged.Value.BatchId);

        var batches = scope.ServiceProvider.GetRequiredService<IImportBatchQueries>();
        var original = await batches.GetAsync(originalId, CancellationToken.None);
        var copy = await batches.GetAsync(restaged.Value.BatchId, CancellationToken.None);
        Assert.Equal(original!.RawFilePath, copy!.RawFilePath);
        Assert.Equal(original.RawFileSha256, copy.RawFileSha256);

        await PromoteAsync(originalId);
        await PromoteAsync(restaged.Value.BatchId);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(4, await db.OrderItems.Where(i => i.Sku == "T-RST-SKU").SumAsync(i => i.Quantity));
    }

    [SqlFact]
    public async Task Promote_UnknownBatch_ReturnsFailure()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IPromotionService>().PromoteAsync(987_654, CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
