using AERai.Web.Application.Bookkeeping;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Bookkeeping;

public sealed class IifExportGeneratorTests
{
    private static readonly DateTimeOffset PeriodEnd = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);

    private static SettlementPreview Preview(string? deposit, params SettlementPreviewRow[] rows) =>
        new("S1", MarketplaceIds.UnitedStates, "USD", PeriodEnd.AddDays(-14), PeriodEnd, rows.Sum(r => r.Amount), deposit, rows,
            AccountTotals: rows.Any(r => r.QuickBooksAccountName is null)
                ? []
                : [.. rows.GroupBy(r => r.QuickBooksAccountName!).Select(g => new SettlementAccountTotal(g.Key, g.Sum(r => r.Amount))).OrderBy(a => a.Account, StringComparer.Ordinal)]);

    [Fact]
    public void Generate_SumsKindsSharingAnAccountAndBalances()
    {
        var preview = Preview(
            "Checking",
            new SettlementPreviewRow("ItemPrice", "Principal", "Amazon Sales", 100m, 100m),
            new SettlementPreviewRow("ItemFees", "Commission", "Amazon Fees", -15m, -15m),
            new SettlementPreviewRow("ItemFees", "FBAPerUnitFulfillmentFee", "Amazon Fees", -3.5m, -3.5m),
            new SettlementPreviewRow("Refund", "Principal", "Amazon Sales", -10m, -10m));

        var lines = IifExportGenerator.Generate(preview).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("!TRNS\tTRNSID\tTRNSTYPE\tDATE\tACCNT\tNAME\tCLASS\tAMOUNT\tDOCNUM\tMEMO", lines[0]);
        Assert.Equal("!ENDTRNS", lines[2]);
        Assert.Equal("TRNS\t1\tGENERAL JOURNAL\t09/28/2026\tChecking\t\t\t71.50\tS1\tAmazon settlement S1", lines[3]);

        // One SPL per account, ordered by name, each the negation of its total; they offset the deposit exactly.
        Assert.Equal("SPL\t1\tGENERAL JOURNAL\t09/28/2026\tAmazon Fees\t\t\t18.50\tS1\tAmazon settlement S1", lines[4]);
        Assert.Equal("SPL\t2\tGENERAL JOURNAL\t09/28/2026\tAmazon Sales\t\t\t-90.00\tS1\tAmazon settlement S1", lines[5]);
        Assert.Equal("ENDTRNS", lines[6]);
        Assert.Equal(7, lines.Length);
    }

    [Fact]
    public void Generate_UnmappedAmounts_Throws()
    {
        var preview = Preview("Checking", new SettlementPreviewRow("ItemFees", "Commission", null, -5m, -5m));

        Assert.Throws<InvalidOperationException>(() => IifExportGenerator.Generate(preview));
    }

    [Fact]
    public void Generate_NoDepositAccount_Throws()
    {
        var preview = Preview(null, new SettlementPreviewRow("ItemPrice", "Principal", "Sales", 5m, 5m));

        Assert.Throws<InvalidOperationException>(() => IifExportGenerator.Generate(preview));
    }

    [Fact]
    public void Generate_NoLines_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => IifExportGenerator.Generate(Preview("Checking")));
    }

    [Fact]
    public void Generate_ConvertedSettlement_WritesTheBooksCurrencyAndNotesTheRate()
    {
        var books = new SettlementBooks(
            "S2", MarketplaceIds.UnitedStates, "CAD", null, PeriodEnd,
            [new SettlementCategoryTotal("ItemPrice", "Principal", 100.01m, 1), new SettlementCategoryTotal("ItemFees", "Commission", -18.50m, 1)],
            "USD", 0.72m, "ECB 2026-09-16");
        var preview = SettlementPreviewBuilder.Build(
            books,
            [
                new BookkeepingAccountMapping { AmountType = "ItemPrice", AmountDescription = "Principal", QuickBooksAccountName = "Sales", UpdatedBy = "t" },
                new BookkeepingAccountMapping { AmountType = "ItemFees", AmountDescription = "Commission", QuickBooksAccountName = "Fees", UpdatedBy = "t" },
            ],
            "Checking");

        var lines = IifExportGenerator.Generate(preview).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        const string memo = "Amazon settlement S2; CAD 81.51 at 0.72 USD (ECB 2026-09-16)";
        Assert.Equal($"TRNS\t1\tGENERAL JOURNAL\t09/28/2026\tChecking\t\t\t58.69\tS2\t{memo}", lines[3]);
        Assert.Equal($"SPL\t1\tGENERAL JOURNAL\t09/28/2026\tFees\t\t\t13.32\tS2\t{memo}", lines[4]);
        Assert.Equal($"SPL\t2\tGENERAL JOURNAL\t09/28/2026\tSales\t\t\t-72.01\tS2\t{memo}", lines[5]);
    }

    [Fact]
    public void Generate_WaitingForARate_Throws()
    {
        var preview = new SettlementPreview("S1", MarketplaceIds.UnitedStates, "CAD", null, PeriodEnd, 5m, "Checking",
            [new SettlementPreviewRow("ItemPrice", "Principal", "Sales", 5m, null)], NeedsRate: true, AccountTotals: []);

        Assert.Throws<InvalidOperationException>(() => IifExportGenerator.Generate(preview));
    }
}
