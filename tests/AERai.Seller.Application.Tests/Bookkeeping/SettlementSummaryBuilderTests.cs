using AERai.Seller.Application.Bookkeeping;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Staging;
using Xunit;

namespace AERai.Seller.Application.Tests.Bookkeeping;

public class SettlementSummaryBuilderTests
{
    private static SettlementReport Settlement(decimal totalAmount = 93.50m) => new()
    {
        SettlementId = "1000",
        MarketplaceId = "Amazon.com",
        FinancialEventGroupStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
        FinancialEventGroupEnd = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero),
        TotalAmount = totalAmount,
        Currency = "USD",
    };

    [Fact]
    public void Build_GroupsLineItems_ByAmountTypeAndDescription()
    {
        var lineItems = new List<SettlementLineItem>
        {
            new() { SettlementId = "1000", AmountType = "ItemPrice", AmountDescription = "Principal", Amount = 60m, Currency = "USD", PostedDate = default },
            new() { SettlementId = "1000", AmountType = "ItemPrice", AmountDescription = "Principal", Amount = 40m, Currency = "USD", PostedDate = default },
            new() { SettlementId = "1000", AmountType = "ItemFees", AmountDescription = "Commission", Amount = -6.50m, Currency = "USD", PostedDate = default },
        };

        var summary = SettlementSummaryBuilder.Build(Settlement(), lineItems, mappings: []);

        Assert.Equal(2, summary.Rows.Count);
        Assert.Equal(100m, summary.Rows.Single(r => r.AmountType == "ItemPrice").Amount);
        Assert.Equal(-6.50m, summary.Rows.Single(r => r.AmountType == "ItemFees").Amount);
    }

    [Fact]
    public void Build_ReportsUnmappedCategories_WhenNoMappingExists()
    {
        var lineItems = new List<SettlementLineItem>
        {
            new() { SettlementId = "1000", AmountType = "ItemPrice", AmountDescription = "Principal", Amount = 100m, Currency = "USD", PostedDate = default },
        };

        var summary = SettlementSummaryBuilder.Build(Settlement(), lineItems, mappings: []);

        Assert.True(summary.HasUnmappedCategories);
        Assert.Single(summary.UnmappedCategories);
        Assert.Null(summary.Rows[0].QuickBooksAccountName);
    }

    [Fact]
    public void Build_ResolvesMappedAccountName_AndExcludesFromUnmapped()
    {
        var lineItems = new List<SettlementLineItem>
        {
            new() { SettlementId = "1000", AmountType = "ItemPrice", AmountDescription = "Principal", Amount = 100m, Currency = "USD", PostedDate = default },
        };
        var mappings = new List<BookkeepingAccountMapping>
        {
            new() { AmountType = "ItemPrice", AmountDescription = "Principal", QuickBooksAccountName = "Sales Income", UpdatedAt = default },
        };

        var summary = SettlementSummaryBuilder.Build(Settlement(), lineItems, mappings);

        Assert.False(summary.HasUnmappedCategories);
        Assert.Equal("Sales Income", summary.Rows[0].QuickBooksAccountName);
    }

    [Fact]
    public void Build_UsesSettlementTotalAmount_AsNetTotal()
    {
        var summary = SettlementSummaryBuilder.Build(Settlement(totalAmount: 42.10m), lineItems: [], mappings: []);

        Assert.Equal(42.10m, summary.NetTotal);
    }
}
