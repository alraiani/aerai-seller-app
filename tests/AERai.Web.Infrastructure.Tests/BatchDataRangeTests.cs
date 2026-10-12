using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Imports;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Staging;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>SQL-backed tests for the date range recorded on each staged batch (<c>stg.usp_SetBatchDataRange</c>).</summary>
public sealed class BatchDataRangeTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private async Task<ImportBatchSummary> StageAsync(ImportSource source, string tsv)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var bytes = Encoding.UTF8.GetBytes(tsv);
        var staged = await scope.ServiceProvider.GetRequiredService<IStagingImportService>().ImportAsync(
            new ImportFileCommand(source, MarketplaceIds.UnitedStates, $"{source}.tsv", bytes.Length, new MemoryStream(bytes), "tests@aeraigroup.com"),
            CancellationToken.None);
        Assert.True(staged.IsSuccess, staged.Error);

        var batch = await scope.ServiceProvider.GetRequiredService<IImportBatchQueries>().GetAsync(staged.Value.BatchId, CancellationToken.None);
        return Assert.IsType<ImportBatchSummary>(batch);
    }

    private static string Day(DateTimeOffset? value) => value!.Value.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    [SqlFact]
    public async Task Orders_RangeIsTheEarliestAndLatestPurchaseDate_InAnyOfTheAcceptedFormats()
    {
        var batch = await StageAsync(ImportSource.Orders,
            "amazon-order-id\tpurchase-date\torder-status\tsku\tquantity\titem-price\n" +
            "R-1\t2026-03-05T10:00:00Z\tShipped\tR-A\t1\t5.00\n" +
            "R-2\t2026-03-01T23:30:00Z\tShipped\tR-A\t1\t5.00\n" +
            "R-3\t24.03.2026 01:50:50 UTC\tShipped\tR-A\t1\t5.00\n" +
            "R-4\tnot a date\tShipped\tR-A\t1\t5.00\n");

        Assert.Equal(("2026-03-01", "2026-03-24"), (Day(batch.DataStart), Day(batch.DataEnd)));
    }

    [SqlFact]
    public async Task Settlements_RangeIsThePeriodFromTheHeaderRow()
    {
        var batch = await StageAsync(ImportSource.Settlements,
            "settlement-id\tsettlement-start-date\tsettlement-end-date\tposted-date\ttransaction-type\tamount-type\tamount\n" +
            "R-S1\t2026-02-01 08:00:00 UTC\t2026-02-15 08:00:00 UTC\t\t\t\t\n" +
            "R-S1\t\t\t2026-02-10\tOrder\tItemPrice\t10.00\n");

        Assert.Equal(("2026-02-01", "2026-02-15"), (Day(batch.DataStart), Day(batch.DataEnd)));
    }

    [SqlFact]
    public async Task Settlements_LinesPostedBeforeThePeriodOpened_DoNotStretchTheRange()
    {
        // Real settlements include lines posted before the period opens (a Dec 29 settlement had lines from Nov 30).
        var batch = await StageAsync(ImportSource.Settlements,
            "settlement-id\tsettlement-start-date\tsettlement-end-date\tposted-date\ttransaction-type\tamount-type\tamount\n" +
            "R-S3\t2025-12-29 01:53:02 UTC\t2026-01-12 01:53:02 UTC\t\t\t\t\n" +
            "R-S3\t\t\t2025-11-30\tOrder\tItemPrice\t10.00\n" +
            "R-S3\t\t\t2026-01-13\tOrder\tItemPrice\t10.00\n");

        Assert.Equal(("2025-12-29", "2026-01-12"), (Day(batch.DataStart), Day(batch.DataEnd)));
    }

    [SqlFact]
    public async Task Settlements_WithoutAPeriod_FallBackToTheLinePostedDates()
    {
        var batch = await StageAsync(ImportSource.Settlements,
            "settlement-id\tposted-date\ttransaction-type\tamount-type\tamount\n" +
            "R-S2\t2026-04-03\tOrder\tItemPrice\t10.00\n" +
            "R-S2\t2026-04-09\tOrder\tItemPrice\t10.00\n");

        Assert.Equal(("2026-04-03", "2026-04-09"), (Day(batch.DataStart), Day(batch.DataEnd)));
    }

    [SqlFact]
    public async Task Inventory_RangeIsTheSnapshotDates()
    {
        var batch = await StageAsync(ImportSource.Inventory,
            "snapshot-date\tsku\tstate\tquantity\n" +
            "2026-05-02\tR-INV\tAvailable\t5\n" +
            "2026-05-04\tR-INV\tAvailable\t6\n");

        Assert.Equal(("2026-05-02", "2026-05-04"), (Day(batch.DataStart), Day(batch.DataEnd)));
    }

    [SqlFact]
    public async Task FbaSnapshot_IsDatedByTheDayItWasReceived()
    {
        var batch = await StageAsync(ImportSource.FbaInventory,
            "sku\tasin\tproduct-name\tafn-fulfillable-quantity\tafn-unsellable-quantity\tafn-reserved-quantity\tafn-inbound-working-quantity\tafn-inbound-shipped-quantity\tafn-inbound-receiving-quantity\n" +
            "R-FBA\tB000000001\tWidget\t5\t0\t0\t0\t0\t0\n");

        Assert.Equal(batch.UploadedAt, batch.DataStart);
        Assert.Equal(batch.UploadedAt, batch.DataEnd);
    }

    [SqlFact]
    public async Task Orders_NoReadableDates_LeavesTheRangeEmpty()
    {
        var batch = await StageAsync(ImportSource.Orders,
            "amazon-order-id\tpurchase-date\torder-status\tsku\tquantity\titem-price\n" +
            "R-9\tgarbage\tShipped\tR-A\t1\t5.00\n");

        Assert.Null(batch.DataStart);
        Assert.Null(batch.DataEnd);
    }
}
