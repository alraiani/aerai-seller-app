using AERai.Seller.Application.Reports;
using Xunit;

namespace AERai.Seller.Application.Tests.Reports;

public class SettlementReportParserTests
{
    private static readonly DateTimeOffset SyncedAt = new(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parse_GroupsRepeatedSettlementHeaderColumns_IntoOneSettlementReport()
    {
        const string tsv =
            "settlement-id\tsettlement-start-date\tsettlement-end-date\tdeposit-date\ttotal-amount\tcurrency\tmarketplace-name\torder-id\tsku\tamount-type\tamount-description\tamount\tposted-date\n" +
            "1000\t2026-07-01T00:00:00Z\t2026-07-14T00:00:00Z\t2026-07-16T00:00:00Z\t93.50\tUSD\tAmazon.com\t111-1\tWIDGET-1\tItemPrice\tPrincipal\t100.00\t2026-07-05T00:00:00Z\n" +
            "1000\t2026-07-01T00:00:00Z\t2026-07-14T00:00:00Z\t2026-07-16T00:00:00Z\t93.50\tUSD\tAmazon.com\t111-1\tWIDGET-1\tItemFees\tCommission\t-6.50\t2026-07-05T00:00:00Z\n";

        var rows = TsvReportParser.Parse(tsv);
        var (settlements, lineItems) = SettlementReportParser.Parse(rows, SyncedAt);

        var settlement = Assert.Single(settlements);
        Assert.Equal("1000", settlement.SettlementId);
        Assert.Equal("Amazon.com", settlement.MarketplaceId);
        Assert.Equal(93.50m, settlement.TotalAmount);
        Assert.Equal("USD", settlement.Currency);
        Assert.Equal(new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), settlement.DepositDate);

        Assert.Equal(2, lineItems.Count);
        Assert.Equal(100.00m, lineItems.Single(l => l.AmountType == "ItemPrice").Amount);
        Assert.Equal(-6.50m, lineItems.Single(l => l.AmountType == "ItemFees").Amount);
        Assert.All(lineItems, l => Assert.Equal("1000", l.SettlementId));
    }

    [Fact]
    public void Parse_SkipsRows_WithoutSettlementId()
    {
        const string tsv = "settlement-id\tamount-type\tamount\n\tItemPrice\t100.00\n";

        var rows = TsvReportParser.Parse(tsv);
        var (settlements, lineItems) = SettlementReportParser.Parse(rows, SyncedAt);

        Assert.Empty(settlements);
        Assert.Empty(lineItems);
    }

    [Fact]
    public void Parse_SkipsSummaryRows_WithoutAmountType()
    {
        // Some settlement flat-file exports include a settlement-level summary row with no
        // amount-type/amount-description — it should register the settlement but not a line item.
        const string tsv =
            "settlement-id\ttotal-amount\tcurrency\tamount-type\tamount\n" +
            "1000\t93.50\tUSD\t\t\n";

        var rows = TsvReportParser.Parse(tsv);
        var (settlements, lineItems) = SettlementReportParser.Parse(rows, SyncedAt);

        Assert.Single(settlements);
        Assert.Empty(lineItems);
    }

    // Exact header of GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE_V2 ("long" layout) from a live SP-API pull.
    private const string V2Header =
        "settlement-id\tsettlement-start-date\tsettlement-end-date\tdeposit-date\ttotal-amount\tcurrency\t" +
        "transaction-type\torder-id\tmerchant-order-id\tadjustment-id\tshipment-id\tmarketplace-name\t" +
        "amount-type\tamount-description\tamount\tfulfillment-id\tposted-date\tposted-date-time\t" +
        "order-item-code\tmerchant-order-item-id\tmerchant-adjustment-item-id\tsku\tquantity-purchased\tpromotion-id\n";

    // Exact header of the non-_V2 GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE ("wide" layout) from a live SP-API pull.
    private const string WideHeader =
        "settlement-id\tsettlement-start-date\tsettlement-end-date\tdeposit-date\ttotal-amount\tcurrency\t" +
        "transaction-type\torder-id\tmerchant-order-id\tadjustment-id\tshipment-id\tmarketplace-name\t" +
        "shipment-fee-type\tshipment-fee-amount\torder-fee-type\torder-fee-amount\tfulfillment-id\tposted-date\t" +
        "order-item-code\tmerchant-order-item-id\tmerchant-adjustment-item-id\tsku\tquantity-purchased\t" +
        "price-type\tprice-amount\titem-related-fee-type\titem-related-fee-amount\tmisc-fee-amount\t" +
        "other-fee-amount\tother-fee-reason-description\tpromotion-id\tpromotion-type\tpromotion-amount\t" +
        "direct-payment-type\tdirect-payment-amount\tother-amount\n";

    [Fact]
    public void Parse_V2LongLayout_ProducesSettlementAndLineItems()
    {
        const string tsv =
            V2Header +
            // Settlement-level summary row: header columns only, no amount-type.
            "1000\t2026-07-01T00:00:00Z\t2026-07-14T00:00:00Z\t2026-07-16T00:00:00Z\t93.50\tUSD\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\n" +
            "1000\t\t\t\t\tUSD\tOrder\t111-1\t\t\tS1\tAmazon.com\tItemPrice\tPrincipal\t100.00\tAFN\t2026-07-05\t2026-07-05T12:00:00Z\tI1\t\t\tWIDGET-1\t1\t\n" +
            "1000\t\t\t\t\tUSD\tOrder\t111-1\t\t\tS1\tAmazon.com\tItemFees\tCommission\t-6.50\tAFN\t2026-07-05\t2026-07-05T12:00:00Z\tI1\t\t\tWIDGET-1\t1\t\n";

        var rows = TsvReportParser.Parse(tsv);
        var (settlements, lineItems) = SettlementReportParser.Parse(rows, SyncedAt);

        var settlement = Assert.Single(settlements);
        Assert.Equal("1000", settlement.SettlementId);
        Assert.Equal(93.50m, settlement.TotalAmount);

        Assert.Equal(2, lineItems.Count);
        var principal = lineItems.Single(l => l.AmountType == "ItemPrice");
        Assert.Equal("Principal", principal.AmountDescription);
        Assert.Equal(100.00m, principal.Amount);
        Assert.Equal("111-1", principal.AmazonOrderId);
        Assert.Equal("WIDGET-1", principal.Sku);
        Assert.Equal(new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero), principal.PostedDate);
        Assert.Equal(-6.50m, lineItems.Single(l => l.AmountType == "ItemFees").Amount);
    }

    [Fact]
    public void Parse_WideLayout_ThrowsInsteadOfSilentlyImportingZeroLineItems()
    {
        const string tsv =
            WideHeader +
            "1000\t2026-07-01T00:00:00Z\t2026-07-14T00:00:00Z\t2026-07-16T00:00:00Z\t93.50\tUSD\n" +
            "1000\t\t\t\t\tUSD\tOrder\t111-1\t\t\tS1\tAmazon.com\t\t\t\t\tAFN\t2026-07-05\tI1\t\t\tWIDGET-1\t1\t" +
            "Principal\t100.00\tCommission\t-6.50\t\t\t\t\t\t\t\t\t\n";

        var rows = TsvReportParser.Parse(tsv);

        var ex = Assert.Throws<FormatException>(() => SettlementReportParser.Parse(rows, SyncedAt));
        Assert.Contains("amount-type", ex.Message);
        Assert.Contains("GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE_V2", ex.Message);
    }

    [Fact]
    public void Parse_EmptyReport_ReturnsNothing_WithoutThrowing()
    {
        var rows = TsvReportParser.Parse(V2Header);

        var (settlements, lineItems) = SettlementReportParser.Parse(rows, SyncedAt);

        Assert.Empty(settlements);
        Assert.Empty(lineItems);
    }
}
