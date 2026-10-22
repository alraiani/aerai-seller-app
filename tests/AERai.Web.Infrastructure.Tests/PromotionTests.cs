using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Core;
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
    private async Task<long> StageAsync(ImportSource source, string fileName, string content, string marketplaceId = MarketplaceIds.UnitedStates)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var bytes = Encoding.UTF8.GetBytes(content);
        var result = await scope.ServiceProvider.GetRequiredService<IStagingImportService>()
            .ImportAsync(new ImportFileCommand(source, marketplaceId, fileName, bytes.Length, new MemoryStream(bytes), "tests@aeraigroup.com"), CancellationToken.None);

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
    public async Task Inventory_RawStatesNormalizeAndSnapshotReplacesPriorStates()
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
            Assert.Equal((3, 4, 0), (position.InboundWorking, position.InboundShipped, position.InboundUnsplit));
            Assert.False(position.HasReservedBreakdown); // a plain "Reserved" label has no reason
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
        Assert.Equal((100.00m, -20.00m, -55.99m, 0m), (summary.Sales, summary.Refunds, summary.Expenses, summary.Reserve));
        Assert.Equal(24.01m, summary.Net);
    }

    [SqlFact]
    public async Task Settlements_ColumnsMatchSellerCentralsStatement()
    {
        // The mix behind Seller Central's statement columns: taxes that cancel out, shipping, a promotion, a
        // reimbursement and its clawback, a refund with its returned fee, and the account level reserve.
        const string tsv =
            "settlement-id\tsettlement-start-date\tsettlement-end-date\ttotal-amount\ttransaction-type\tamount-type\tamount-description\tamount\tcurrency\n" +
            "T-SET-A\t2026-06-01 01:53:02 UTC\t2026-06-15 01:53:02 UTC\t78.33\t\t\t\t\tUSD\n" +
            "T-SET-A\t\t\t\tOrder\tItemPrice\tPrincipal\t100.00\t\n" +
            "T-SET-A\t\t\t\tOrder\tItemPrice\tTax\t7.00\t\n" +
            "T-SET-A\t\t\t\tOrder\tItemWithheldTax\tMarketplaceFacilitatorTax-Principal\t-7.00\t\n" +
            "T-SET-A\t\t\t\tOrder\tItemPrice\tShipping\t5.00\t\n" +
            "T-SET-A\t\t\t\tOrder\tPromotion\tShipping\t-5.00\t\n" +
            "T-SET-A\t\t\t\tOrder\tItemFees\tCommission\t-15.00\t\n" +
            "T-SET-A\t\t\t\tother-transaction\tFBA Inventory Reimbursement\tWAREHOUSE_LOST\t12.00\t\n" +
            "T-SET-A\t\t\t\tother-transaction\tFBA Inventory Reimbursement\tCOMPENSATED_CLAWBACK\t-4.00\t\n" +
            "T-SET-A\t\t\t\tRefund\tItemPrice\tPrincipal\t-20.00\t\n" +
            "T-SET-A\t\t\t\tRefund\tItemFees\tCommission\t3.00\t\n" +
            "T-SET-A\t\t\t\tother-transaction\tother-transaction\tPrevious Reserve Amount Balance\t9.32\t\n" +
            "T-SET-A\t\t\t\tother-transaction\tother-transaction\tCurrent Reserve Amount\t-6.99\t\n";

        await PromoteAsync(await StageAsync(ImportSource.Settlements, "settlement-statement.tsv", tsv));

        await using var scope = fixture.Services.CreateAsyncScope();
        var summary = await scope.ServiceProvider.GetRequiredService<AppDbContext>().SettlementSummaries.SingleAsync(s => s.SettlementId == "T-SET-A");

        Assert.Equal((117.00m, -17.00m, -24.00m, 2.33m), (summary.Sales, summary.Refunds, summary.Expenses, summary.Reserve));
        Assert.Equal(78.33m, summary.Net); // The header's total-amount: the four columns add up to the deposit.
    }

    [SqlFact]
    public async Task Settlements_SkuSummaryGroupsByPricePerItem_AndLinesExplainEachGroup()
    {
        const string tsv =
            "settlement-id\tsettlement-start-date\tsettlement-end-date\ttransaction-type\torder-id\torder-item-code\tsku\tquantity-purchased\tamount-type\tamount-description\tamount\tcurrency\n" +
            "T-SET-2\t2026-09-04 07:00:00 UTC\t2026-09-18 07:00:00 UTC\t\t\t\t\t\t\t\t\tUSD\n" +
            "T-SET-2\t\t\tOrder\tT-O-1\tI1\tT-SKU-A\t1\tItemPrice\tPrincipal\t10.00\t\n" +
            "T-SET-2\t\t\tOrder\tT-O-1\tI1\tT-SKU-A\t\tItemFees\tCommission\t-1.50\t\n" +
            "T-SET-2\t\t\tOrder\tT-O-2\tI2\tT-SKU-A\t2\tItemPrice\tPrincipal\t20.00\t\n" +
            "T-SET-2\t\t\tOrder\tT-O-2\tI2\tT-SKU-A\t\tItemFees\tCommission\t-3.00\t\n" +
            "T-SET-2\t\t\tOrder\tT-O-3\tI3\tT-SKU-A\t1\tItemPrice\tPrincipal\t12.00\t\n" +
            "T-SET-2\t\t\tRefund\tT-O-4\tI4\tT-SKU-A\t\tItemPrice\tPrincipal\t-10.00\t\n" +
            "T-SET-2\t\t\tServiceFee\t\t\t\t\tother-transaction\tSubscription\t-39.99\t\n";

        var batchId = await StageAsync(ImportSource.Settlements, "settlement-sku.tsv", tsv);
        await PromoteAsync(batchId);

        await using var scope = fixture.Services.CreateAsyncScope();
        var reporting = scope.ServiceProvider.GetRequiredService<IReportingQueries>();
        var marketplaceId = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Settlements.SingleAsync(s => s.SettlementId == "T-SET-2")).MarketplaceId;

        var rows = await reporting.GetSettlementSkuSummaryAsync(marketplaceId, "T-SET-2", new PageRequest(), null, CancellationToken.None);

        // A 2-unit order of 20.00 is 10.00 each, so it shares a row with the 1-unit 10.00 order and the 10.00 refund
        // (the refund in its own column, with no quantity of its own); the 12.00 sale and the SKU-less fee are separate rows.
        Assert.Equal(3, rows.TotalCount);
        var tenDollar = rows.Items.Single(r => r.Sku == "T-SKU-A" && r.SalePrice == 10.00m);
        Assert.Equal((3, 3, 30.00m, -4.50m, -10.00m, 15.50m), (tenDollar.Units, tenDollar.OrderCount, tenDollar.Sales, tenDollar.Expenses, tenDollar.Refunds, tenDollar.Net));
        Assert.Equal((1, 12.00m), (rows.Items.Single(r => r.SalePrice == 12.00m).Units, rows.Items.Single(r => r.SalePrice == 12.00m).Sales));
        Assert.Equal((0, -39.99m), (rows.Items.Single(r => r.Sku is null).Units, rows.Items.Single(r => r.Sku is null).Net));

        // The settlement total is the sum of its rows.
        var settlement = await reporting.GetSettlementAsync(marketplaceId, "T-SET-2", CancellationToken.None);
        Assert.Equal(rows.Items.Sum(r => r.Net), settlement!.Net);
        Assert.Equal(4, settlement.Units);

        // The drill-down groups every line of the row by order, with amounts in the bucket columns, and adds up to the row.
        var groups = await reporting.GetSettlementOrderGroupsAsync(marketplaceId, "T-SET-2", "T-SKU-A", 10.00m, new PageRequest(), null, CancellationToken.None);
        var lines = groups.Items.SelectMany(g => g.Lines).ToList();
        Assert.Equal(["T-O-1", "T-O-2", "T-O-4"], groups.Items.Select(g => g.AmazonOrderId).Order());
        Assert.Equal(tenDollar.LineCount, lines.Count);
        Assert.Equal(tenDollar.Net, groups.Items.Sum(g => g.Amount));
        Assert.Equal(tenDollar.Units, groups.Items.Sum(g => g.Units));
        Assert.All(groups.Items, g => Assert.Equal(g.Amount, g.Lines.Sum(l => l.Amount)));
        Assert.All(lines.Where(l => l.AmountType == "ItemFees"), l => Assert.Equal(l.Amount, l.Expenses));
    }

    [SqlFact]
    public async Task SettlementQueries_SearchFilterAndSort_SelectAndOrderTheRightRows()
    {
        const string header =
            "settlement-id\tsettlement-start-date\tsettlement-end-date\ttransaction-type\torder-id\torder-item-code\tsku\tquantity-purchased\tamount-type\tamount-description\tamount\tcurrency\n";
        const string early =
            "T-SET-5\t2026-07-01 07:00:00 UTC\t2026-07-15 07:00:00 UTC\t\t\t\t\t\t\t\t\tUSD\n" +
            "T-SET-5\t\t\tOrder\tT-O-50\tI50\tT-SKU-E\t1\tItemPrice\tPrincipal\t100.00\t\n" +
            "T-SET-5\t\t\tOrder\tT-O-50\tI50\tT-SKU-E\t\tItemFees\tCommission\t-15.00\t\n";
        const string late =
            "T-SET-6\t2026-08-01 07:00:00 UTC\t2026-08-15 07:00:00 UTC\t\t\t\t\t\t\t\t\tUSD\n" +
            "T-SET-6\t\t\tOrder\tT-O-60\tI60\tT-SKU-F\t1\tItemPrice\tPrincipal\t10.00\t\n" +
            "T-SET-6\t\t\tOrder\tT-O-60\tI60\tT-SKU-F\t\tItemFees\tCommission\t-1.50\t\n" +
            "T-SET-6\t\t\tRefund\tT-O-60\tI60\tT-SKU-F\t\tItemPrice\tPrincipal\t-10.00\t\n" +
            "T-SET-6\t\t\tOrder\tT-O-61\tI61\tT-SKU-F\t1\tItemPrice\tPrincipal\t10.00\t\n" +
            "T-SET-6\t\t\tServiceFee\t\t\t\t\tother-transaction\tSubscription\t-39.99\t\n" +
            "T-SET-6\t\t\tServiceFee\t\t\t\t\tother-transaction\tStorage Fee\t-5.00\t\n";

        await PromoteAsync(await StageAsync(ImportSource.Settlements, "settlement-early.tsv", header + early));
        await PromoteAsync(await StageAsync(ImportSource.Settlements, "settlement-late.tsv", header + late));

        await using var scope = fixture.Services.CreateAsyncScope();
        var reporting = scope.ServiceProvider.GetRequiredService<IReportingQueries>();
        var marketplaceId = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Settlements.SingleAsync(s => s.SettlementId == "T-SET-6")).MarketplaceId;
        var ct = CancellationToken.None;

        // Search finds a settlement by an order id or SKU inside it, not only by its own id.
        var byOrder = await reporting.GetSettlementsAsync(marketplaceId, new PageRequest(Search: "T-O-50"), null, ct);
        Assert.Equal("T-SET-5", Assert.Single(byOrder.Items).SettlementId);
        var bySku = await reporting.GetSettlementsAsync(marketplaceId, new PageRequest(Search: "T-SKU-F"), null, ct);
        Assert.Equal("T-SET-6", Assert.Single(bySku.Items).SettlementId);

        // The period range filters on the period end; totals cover what the filter selects.
        var august = new SettlementListFilter(PeriodEndFrom: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), PeriodEndBefore: new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        var inAugust = await reporting.GetSettlementsAsync(marketplaceId, new PageRequest(Search: "T-SET-"), august, ct);
        Assert.Equal("T-SET-6", Assert.Single(inAugust.Items).SettlementId);
        var totals = await reporting.GetSettlementTotalsAsync(marketplaceId, new PageRequest(Search: "T-SET-"), august, ct);
        Assert.NotNull(totals);
        Assert.Equal(("USD", 1, inAugust.Items[0].Net), (totals.Currency, totals.Count, totals.Net));

        // Sorting by net puts the larger payout first, and reversing flips it.
        var ours = new PageRequest(Search: "T-SET-");
        var byNet = await reporting.GetSettlementsAsync(marketplaceId, ours, new SettlementListFilter(Sort: SettlementSort.Net), ct);
        Assert.Equal(["T-SET-5", "T-SET-6"], byNet.Items.Where(s => s.SettlementId is "T-SET-5" or "T-SET-6").Select(s => s.SettlementId));
        var byNetReversed = await reporting.GetSettlementsAsync(marketplaceId, ours, new SettlementListFilter(Sort: SettlementSort.Net, Descending: true), ct);
        Assert.Equal(["T-SET-6", "T-SET-5"], byNetReversed.Items.Where(s => s.SettlementId is "T-SET-5" or "T-SET-6").Select(s => s.SettlementId));

        // Summary kinds: the refunded row, and the row without a SKU.
        var refunded = await reporting.GetSettlementSkuSummaryAsync(marketplaceId, "T-SET-6", new PageRequest(), new SettlementSkuFilter(SettlementRowKind.WithRefunds), ct);
        Assert.Equal("T-SKU-F", Assert.Single(refunded.Items).Sku);
        var noSku = await reporting.GetSettlementSkuSummaryAsync(marketplaceId, "T-SET-6", new PageRequest(), new SettlementSkuFilter(SettlementRowKind.NoSku), ct);
        Assert.Null(Assert.Single(noSku.Items).Sku);

        // Lines without an order are grouped by transaction type.
        var fees = await reporting.GetSettlementOrderGroupsAsync(marketplaceId, "T-SET-6", null, null, new PageRequest(), null, ct);
        var serviceFees = Assert.Single(fees.Items);
        Assert.Equal(("ServiceFee", false, 2, -44.99m), (serviceFees.TransactionType, serviceFees.IsOrder, serviceFees.Lines.Count, serviceFees.Amount));

        // The refund bucket and a search both pick whole groups: the refunded order keeps its sale and fee lines.
        var price = refunded.Items[0].SalePrice;
        var withRefund = await reporting.GetSettlementOrderGroupsAsync(marketplaceId, "T-SET-6", "T-SKU-F", price, new PageRequest(), new SettlementLineFilter(SettlementBucket.Refunds), ct);
        var refundedOrder = Assert.Single(withRefund.Items);
        Assert.Equal(("T-O-60", 3, -1.50m), (refundedOrder.AmazonOrderId, refundedOrder.Lines.Count, refundedOrder.Amount));
        var searched = await reporting.GetSettlementOrderGroupsAsync(marketplaceId, "T-SET-6", "T-SKU-F", price, new PageRequest(Search: "Commission"), null, ct);
        Assert.Equal("T-O-60", Assert.Single(searched.Items).AmazonOrderId);

        // Sorting groups by amount puts the larger first.
        var byAmount = await reporting.GetSettlementOrderGroupsAsync(marketplaceId, "T-SET-6", "T-SKU-F", price, new PageRequest(), new SettlementLineFilter(Sort: SettlementGroupSort.Amount), ct);
        Assert.Equal(["T-O-61", "T-O-60"], byAmount.Items.Select(g => g.AmazonOrderId));
    }

    [SqlFact]
    public async Task BackfillSettlementUnits_FillsLinesImportedBeforeUnitsWereKept_AndIsRepeatable()
    {
        // The real settlement flat file V2 layout: the backfill reads fields by position, so a compact test file would be skipped.
        static string Line(string transactionType, string orderId, string itemCode, string sku, string quantity, string amountType, string description, string amount) =>
            string.Join("\t", "T-SET-3", "", "", "", "", "", transactionType, orderId, "", "", "", "Amazon.com", amountType, description, amount, "AFN", "2026-09-05", "", itemCode, "", "", sku, quantity, "");
        var tsv =
            "settlement-id\tsettlement-start-date\tsettlement-end-date\tdeposit-date\ttotal-amount\tcurrency\ttransaction-type\torder-id\tmerchant-order-id\tadjustment-id\tshipment-id\tmarketplace-name\tamount-type\tamount-description\tamount\tfulfillment-id\tposted-date\tposted-date-time\torder-item-code\tmerchant-order-item-id\tmerchant-adjustment-item-id\tsku\tquantity-purchased\tpromotion-id\n" +
            "T-SET-3\t2026-09-04 07:00:00 UTC\t2026-09-18 07:00:00 UTC\t\t0\tUSD\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\n" +
            Line("Order", "T-O-9", "I9", "T-SKU-B", "3", "ItemPrice", "Principal", "30.00") + "\n" +
            Line("Order", "T-O-9", "I9", "T-SKU-B", "", "ItemFees", "Commission", "-4.50") + "\n";

        var batchId = await StageAsync(ImportSource.Settlements, "settlement-backfill.tsv", tsv);
        await PromoteAsync(batchId);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Put the settlement back the way it was before the columns existed.
        await db.Database.ExecuteSqlRawAsync("UPDATE stg.SettlementLine SET OrderItemCode = NULL, QuantityPurchased = NULL WHERE SettlementId = 'T-SET-3'");
        await db.Database.ExecuteSqlRawAsync("UPDATE core.SettlementLine SET OrderItemCode = NULL, Quantity = NULL WHERE SettlementId = 'T-SET-3'");
        var script = SqlResource.Read("V015_BackfillSettlementUnits", "backfill-settlement-units.sql");

        await db.Database.ExecuteSqlRawAsync(script);
        await db.Database.ExecuteSqlRawAsync(script);

        var lines = await db.SettlementLines.AsNoTracking().Where(l => l.SettlementId == "T-SET-3").ToListAsync();
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Equal("I9", l.OrderItemCode));
        Assert.Equal(3, lines.Single(l => l.AmountDescription == "Principal").Quantity);

        var row = await db.SettlementSkuSummaries.AsNoTracking().SingleAsync(r => r.SettlementId == "T-SET-3" && r.Sku == "T-SKU-B");
        Assert.Equal((3, 10.00m), (row.Units, row.SalePrice));
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
    public async Task Settlements_DayFirstDates_AreParsedDayFirstNotMisreadMonthFirst()
    {
        // Real CA/MX settlement files write dates day-first; '07.09.2026' is 7 September, not July 9.
        const string tsv =
            "settlement-id\tsettlement-start-date\tsettlement-end-date\ttransaction-type\tamount-type\tamount\tposted-date\tcurrency\n" +
            "T-SET-DMY\t24.08.2026 01:50:50 UTC\t07.09.2026 01:50:49 UTC\t\t\t\t\tCAD\n" +
            "T-SET-DMY\t\t\tOrder\tItemPrice\t12.00\t05.09.2026\t\n";

        var batchId = await StageAsync(ImportSource.Settlements, "settlement-ca.tsv", tsv);
        await PromoteAsync(batchId);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settlement = await db.Settlements.SingleAsync(s => s.SettlementId == "T-SET-DMY");
        Assert.Equal(new DateTimeOffset(2026, 8, 24, 1, 50, 50, TimeSpan.Zero), settlement.PeriodStart);
        Assert.Equal(new DateTimeOffset(2026, 9, 7, 1, 50, 49, TimeSpan.Zero), settlement.PeriodEnd);
        Assert.Equal(new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.Zero), (await db.SettlementLines.SingleAsync(l => l.SettlementId == "T-SET-DMY")).PostedDate);
    }

    [SqlFact]
    public async Task Promote_UnknownBatch_ReturnsFailure()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IPromotionService>().PromoteAsync(987_654, CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
