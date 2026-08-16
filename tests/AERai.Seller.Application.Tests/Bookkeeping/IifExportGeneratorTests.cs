using AERai.Seller.Application.Bookkeeping;
using AERai.Seller.Domain.Staging;
using Xunit;

namespace AERai.Seller.Application.Tests.Bookkeeping;

public class IifExportGeneratorTests
{
    private static SettlementReport Settlement(decimal totalAmount) => new()
    {
        SettlementId = "1000",
        MarketplaceId = "Amazon.com",
        FinancialEventGroupStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
        FinancialEventGroupEnd = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero),
        TotalAmount = totalAmount,
        Currency = "USD",
        DepositDate = new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void Generate_Throws_WhenSummaryHasUnmappedCategories()
    {
        var summary = new SettlementSummary(
            "1000", "USD", 100m,
            [new SettlementSummaryRow("ItemPrice", "Principal", null, 100m)],
            [("ItemPrice", "Principal")]);

        Assert.Throws<InvalidOperationException>(() => IifExportGenerator.Generate(summary, Settlement(100m), "Amazon Clearing"));
    }

    [Fact]
    public void Generate_Throws_WhenDepositAccountIsMissing()
    {
        var summary = new SettlementSummary("1000", "USD", 100m, [], []);

        Assert.Throws<InvalidOperationException>(() => IifExportGenerator.Generate(summary, Settlement(100m), ""));
    }

    [Fact]
    public void Generate_BalancesExactlyToNetTotal_WhenLineItemsSumToSettlementTotal()
    {
        var summary = new SettlementSummary(
            "1000", "USD", 93.50m,
            [
                new SettlementSummaryRow("ItemPrice", "Principal", "Sales Income", 100.00m),
                new SettlementSummaryRow("ItemFees", "Commission", "Amazon Fees", -6.50m),
            ],
            []);

        var iif = IifExportGenerator.Generate(summary, Settlement(93.50m), "Amazon Clearing");

        var trnsAmount = ExtractAmount(iif, "Amazon Clearing");
        var salesAmount = ExtractAmount(iif, "Sales Income");
        var feesAmount = ExtractAmount(iif, "Amazon Fees");

        Assert.Equal(93.50m, trnsAmount);
        Assert.Equal(trnsAmount, -(salesAmount + feesAmount));
        // No rounding/adjustment line should appear since the mapped rows already sum to NetTotal.
        Assert.DoesNotContain("Unclassified settlement adjustment", iif);
    }

    [Fact]
    public void Generate_AddsRoundingLine_WhenMappedRowsDoNotSumToNetTotal()
    {
        // Deliberately mismatched: rows sum to 90.00 but the settlement's NetTotal is 93.50 —
        // the generator must still balance exactly via a labeled adjustment line.
        var summary = new SettlementSummary(
            "1000", "USD", 93.50m,
            [new SettlementSummaryRow("ItemPrice", "Principal", "Sales Income", 90.00m)],
            []);

        var iif = IifExportGenerator.Generate(summary, Settlement(93.50m), "Amazon Clearing");

        Assert.Contains("Unclassified settlement adjustment", iif);

        var lines = iif.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.StartsWith("TRNS\t") || l.StartsWith("SPL\t"))
            .ToList();
        var totalAmount = lines.Sum(l => decimal.Parse(l.Split('\t')[7]));

        Assert.Equal(0m, totalAmount);
    }

    private static decimal ExtractAmount(string iif, string account)
    {
        var line = iif.Split('\n', StringSplitOptions.RemoveEmptyEntries).Single(l => l.Contains($"\t{account}\t"));
        return decimal.Parse(line.Split('\t')[7]);
    }
}
