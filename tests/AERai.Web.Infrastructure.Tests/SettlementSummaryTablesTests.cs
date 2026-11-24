using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Imports;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Staging;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// The settlement summary tables (V024): promotion fills them, re-promotion replaces them, and the views
/// built on them return exactly what the V021 views computed from the lines on every read.
/// </summary>
public sealed class SettlementSummaryTablesTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private const string Header =
        "settlement-id\tsettlement-start-date\tsettlement-end-date\tdeposit-date\ttransaction-type\torder-id\torder-item-code\tsku\tquantity-purchased\tamount-type\tamount-description\tamount\tcurrency\n";

    /// <summary>One settlement with a line for every bucket and category, including a 2-unit item and an ad charge.</summary>
    private static string Settlement(string id, string currency) =>
        Header +
        $"{id}\t2026-09-04 07:00:00 UTC\t2026-09-18 07:00:00 UTC\t2026-09-20 07:00:00 UTC\t\t\t\t\t\t\t\t\t{currency}\n" +
        $"{id}\t\t\t\tOrder\t{id}-O1\tI1\tSST-A\t2\tItemPrice\tPrincipal\t20.00\t\n" +
        $"{id}\t\t\t\tOrder\t{id}-O1\tI1\tSST-A\t\tItemPrice\tTax\t1.60\t\n" +
        $"{id}\t\t\t\tOrder\t{id}-O1\tI1\tSST-A\t\tItemWithheldTax\tMarketplaceFacilitatorTax-Principal\t-1.60\t\n" +
        $"{id}\t\t\t\tOrder\t{id}-O1\tI1\tSST-A\t\tItemFees\tCommission\t-3.00\t\n" +
        $"{id}\t\t\t\tOrder\t{id}-O1\tI1\tSST-A\t\tItemFees\tFBAPerUnitFulfillmentFee\t-6.20\t\n" +
        $"{id}\t\t\t\tOrder\t{id}-O2\tI2\tSST-B\t1\tItemPrice\tPrincipal\t15.00\t\n" +
        $"{id}\t\t\t\tOrder\t{id}-O2\tI2\tSST-B\t\tItemFees\tShippingChargeback\t-2.00\t\n" +
        $"{id}\t\t\t\tRefund\t{id}-O3\tI3\tSST-A\t\tItemPrice\tPrincipal\t-10.00\t\n" +
        $"{id}\t\t\t\tRefund\t{id}-O3\tI3\tSST-A\t\tItemFees\tCommission\t1.20\t\n" +
        $"{id}\t\t\t\tother-transaction\t\t\t\t\tCost of Advertising\tTransactionTotalAmount\t-25.00\t\n" +
        $"{id}\t\t\t\tother-transaction\t\t\t\t\tFBA Inventory Reimbursement\tWAREHOUSE_DAMAGE\t8.00\t\n" +
        $"{id}\t\t\t\tServiceFee\t\t\t\t\tother-transaction\tSubscription\t-39.99\t\n" +
        $"{id}\t\t\t\tother-transaction\t\t\t\t\tother-transaction\tCurrent Reserve Amount\t-5.00\t\n";

    private async Task PromoteAsync(string fileName, string content)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var bytes = Encoding.UTF8.GetBytes(content);
        var staged = await scope.ServiceProvider.GetRequiredService<IStagingImportService>()
            .ImportAsync(new ImportFileCommand(ImportSource.Settlements, MarketplaceIds.UnitedStates, fileName, bytes.Length, new MemoryStream(bytes), "tests@aeraigroup.com"), CancellationToken.None);
        Assert.True(staged.IsSuccess, staged.Error);
        var promoted = await scope.ServiceProvider.GetRequiredService<IPromotionService>().PromoteAsync(staged.Value.BatchId, CancellationToken.None);
        Assert.True(promoted.IsSuccess, promoted.Error);
    }

    private static Task<int> CountAsync(AppDbContext db, string sql) =>
        db.Database.SqlQueryRaw<int>(sql).SingleAsync();

    [SqlFact]
    public async Task Promote_FillsTheTables_AndRepromotingReplacesTheirRows()
    {
        await PromoteAsync("sst-1.tsv", Settlement("SST-1", "USD"));
        await PromoteAsync("sst-1-again.tsv", Settlement("SST-1", "USD"));

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lines = await db.SettlementLines.CountAsync(l => l.SettlementId == "SST-1");

        Assert.Equal(13, lines);
        Assert.Equal(lines, await CountAsync(db, "SELECT COUNT(*) AS Value FROM rpt.SettlementLineCalc WHERE SettlementId = N'SST-1'"));
        Assert.Equal(1, await CountAsync(db, "SELECT COUNT(*) AS Value FROM rpt.SettlementTotals WHERE SettlementId = N'SST-1'"));
        // SST-A at 10.00 each (the 2-unit sale and its refund), SST-B at 15.00, and the SKU-less charges.
        Assert.Equal(3, await CountAsync(db, "SELECT COUNT(*) AS Value FROM rpt.SettlementSkuTotals WHERE SettlementId = N'SST-1'"));

        var summary = await db.SettlementSummaries.AsNoTracking().SingleAsync(s => s.SettlementId == "SST-1");
        Assert.Equal((35.00m + 8.00m, -8.80m, -5.00m), (summary.Sales, summary.Refunds, summary.Reserve));
        Assert.Equal((-25.00m, -2.00m, -6.20m, -3.00m), (summary.AdCosts, summary.Shipping, summary.FbaFees, summary.Commission));
        Assert.Equal(summary.Sales + summary.Refunds + summary.Expenses + summary.Reserve, summary.Net);
    }

    [SqlFact]
    public async Task Views_ReturnExactlyWhatTheV021ViewsComputedFromTheLines()
    {
        await PromoteAsync("sst-usd.tsv", Settlement("SST-USD", "USD"));
        await PromoteAsync("sst-cad.tsv", Settlement("SST-CAD", "CAD"));

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The V021 views, which did all the work on every read, side by side under other names.
        foreach (var file in new[] { "rpt.vw_SettlementLine.sql", "rpt.vw_SettlementSummary.sql", "rpt.vw_SettlementSkuSummary.sql" })
        {
            var sql = SqlResource.Read("V021_SettlementExpenseBreakdown", file)
                .Replace("rpt.vw_SettlementLine", "rpt.vw_V021SettlementLine", StringComparison.Ordinal)
                .Replace("rpt.vw_SettlementSummary", "rpt.vw_V021SettlementSummary", StringComparison.Ordinal)
                .Replace("rpt.vw_SettlementSkuSummary", "rpt.vw_V021SettlementSkuSummary", StringComparison.Ordinal);
            await db.Database.ExecuteSqlRawAsync(sql);
        }

        foreach (var view in new[] { "SettlementLine", "SettlementSummary", "SettlementSkuSummary" })
        {
            var rows = await CountAsync(db, $"SELECT COUNT(*) AS Value FROM rpt.vw_{view} WHERE SettlementId LIKE N'SST-%'");
            var differences = await CountAsync(db, $"""
                SELECT COUNT(*) AS Value FROM (
                    (SELECT * FROM rpt.vw_{view} WHERE SettlementId LIKE N'SST-%'
                     EXCEPT SELECT * FROM rpt.vw_V021{view} WHERE SettlementId LIKE N'SST-%')
                    UNION ALL
                    (SELECT * FROM rpt.vw_V021{view} WHERE SettlementId LIKE N'SST-%'
                     EXCEPT SELECT * FROM rpt.vw_{view} WHERE SettlementId LIKE N'SST-%')
                ) AS d
                """);

            Assert.True(rows > 0, $"rpt.vw_{view} returned no rows to compare.");
            Assert.True(differences == 0, $"rpt.vw_{view} differs from V021 in {differences} row(s).");
        }
    }
}
