using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Staging;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// Marketplace scoping across the stg → core → rpt flow: promotion stamps the batch's marketplace,
/// skips other channels' order lines without rejecting them, keeps each marketplace's inventory separate, and the views
/// and queries return one marketplace at a time.
/// </summary>
public sealed class MarketplacePromotionTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private const string Us = MarketplaceIds.UnitedStates;
    private const string Ca = MarketplaceIds.Canada;

    private async Task<PromotionSummary> StageAndPromoteAsync(ImportSource source, string marketplaceId, string content)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var bytes = Encoding.UTF8.GetBytes(content);
        var staged = await scope.ServiceProvider.GetRequiredService<IStagingImportService>()
            .ImportAsync(new ImportFileCommand(source, marketplaceId, "file.tsv", bytes.Length, new MemoryStream(bytes), "tests@aeraigroup.com"), CancellationToken.None);
        Assert.True(staged.IsSuccess, staged.Error);

        var promoted = await scope.ServiceProvider.GetRequiredService<IPromotionService>().PromoteAsync(staged.Value.BatchId, CancellationToken.None);
        Assert.True(promoted.IsSuccess, promoted.Error);
        return promoted.Value;
    }

    [SqlFact]
    public async Task Orders_AreStampedWithTheBatchMarketplace_AndOtherChannelsAreSkipped()
    {
        const string tsv =
            "amazon-order-id\tpurchase-date\torder-status\tsku\tquantity\titem-price\tcurrency\tsales-channel\n" +
            "M-CA-1\t2026-10-01T10:00:00Z\tShipped\tM-SKU-1\t1\t30.00\tCAD\tAmazon.ca\n" +
            "M-US-1\t2026-10-01T10:00:00Z\tShipped\tM-SKU-1\t1\t20.00\tUSD\tAmazon.com\n" +
            "M-CA-2\t2026-10-01T11:00:00Z\tShipped\tM-SKU-1\t1\t31.00\tCAD\t\n";

        var result = await StageAndPromoteAsync(ImportSource.Orders, Ca, tsv);

        // Amazon's orders report covers the whole region, so a US line in a CA batch is routine, not an error.
        Assert.Equal((2, 0, 1), (result.PromotedRowCount, result.RejectedRowCount, result.SkippedRowCount));
        Assert.Equal(ImportBatchStatus.Promoted, result.Status);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal([Ca, Ca], await db.Orders.Where(o => o.AmazonOrderId.StartsWith("M-")).Select(o => o.MarketplaceId).ToListAsync());

        var orders = scope.ServiceProvider.GetRequiredService<IReportingQueries>();
        var caOrders = await orders.GetOrdersAsync(Ca, new PageRequest(1, 50, "M-"), CancellationToken.None);
        var usOrders = await orders.GetOrdersAsync(Us, new PageRequest(1, 50, "M-"), CancellationToken.None);
        Assert.Equal(2, caOrders.TotalCount);
        Assert.Equal(0, usOrders.TotalCount);
    }

    [SqlFact]
    public async Task Inventory_SameSkuInTwoMarketplaces_IsKeptSeparately()
    {
        const string header = "snapshot-date\tsku\tstate\tquantity\n";
        await StageAndPromoteAsync(ImportSource.Inventory, Us, header + "2026-10-02\tM-INV-1\tAvailable\t100\n");
        await StageAndPromoteAsync(ImportSource.Inventory, Ca, header + "2026-10-02\tM-INV-1\tAvailable\t7\n");

        // Re-reporting Canada must not touch the US snapshot for the same SKU and date.
        await StageAndPromoteAsync(ImportSource.Inventory, Ca, header + "2026-10-02\tM-INV-1\tAvailable\t6\n");

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var positions = await db.InventoryPositions.Where(p => p.Sku == "M-INV-1").OrderBy(p => p.MarketplaceId).ToListAsync();
        Assert.Equal([(Ca, 6), (Us, 100)], positions.Select(p => (p.MarketplaceId, p.Available)));
    }

    [SqlFact]
    public async Task ProductCost_IsPerMarketplace()
    {
        const string header = "snapshot-date\tsku\tstate\tquantity\n";
        await StageAndPromoteAsync(ImportSource.Inventory, Us, header + "2026-10-02\tM-COST-1\tAvailable\t1\n");

        await using var scope = fixture.Services.CreateAsyncScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var now = DateTimeOffset.UtcNow;
        Assert.True(await products.UpdateCostAsync("M-COST-1", Us, 4.00m, now, CancellationToken.None));
        Assert.True(await products.UpdateCostAsync("M-COST-1", Ca, 5.25m, now, CancellationToken.None));
        Assert.True(await products.UpdateCostAsync("M-COST-1", Us, 4.10m, now, CancellationToken.None));

        Assert.Equal(4.10m, (await products.GetAsync("M-COST-1", Us, CancellationToken.None))!.CostOfGoods);
        Assert.Equal(5.25m, (await products.GetAsync("M-COST-1", Ca, CancellationToken.None))!.CostOfGoods);

        Assert.True(await products.UpdateCostAsync("M-COST-1", Ca, null, now, CancellationToken.None));
        Assert.Null((await products.GetAsync("M-COST-1", Ca, CancellationToken.None))!.CostOfGoods);
        Assert.Equal(4.10m, (await products.GetAsync("M-COST-1", Us, CancellationToken.None))!.CostOfGoods);
        Assert.False(await products.UpdateCostAsync("NO-SUCH-SKU", Us, 1m, now, CancellationToken.None));

        var usList = await products.ListAsync(Us, new PageRequest(1, 50, "M-COST-"), CancellationToken.None);
        var caList = await products.ListAsync(Ca, new PageRequest(1, 50, "M-COST-"), CancellationToken.None);
        Assert.Equal(4.10m, Assert.Single(usList.Items).CostOfGoods);
        Assert.Null(Assert.Single(caList.Items).CostOfGoods);
    }
}
