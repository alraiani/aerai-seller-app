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
}
