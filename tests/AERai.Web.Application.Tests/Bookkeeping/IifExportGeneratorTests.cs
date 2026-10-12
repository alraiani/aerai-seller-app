using AERai.Web.Application.Bookkeeping;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Bookkeeping;

public sealed class IifExportGeneratorTests
{
    private static readonly DateTimeOffset PeriodEnd = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);

    private static SettlementPreview Preview(string? deposit, params SettlementPreviewRow[] rows) =>
        new("S1", MarketplaceIds.UnitedStates, "USD", PeriodEnd.AddDays(-14), PeriodEnd, rows.Sum(r => r.Amount), deposit, rows);

    [Fact]
    public void Generate_SumsKindsSharingAnAccountAndBalances()
    {
        var preview = Preview(
            "Checking",
            new SettlementPreviewRow("ItemPrice", "Principal", "Amazon Sales", 100m),
            new SettlementPreviewRow("ItemFees", "Commission", "Amazon Fees", -15m),
            new SettlementPreviewRow("ItemFees", "FBAPerUnitFulfillmentFee", "Amazon Fees", -3.5m),
            new SettlementPreviewRow("Refund", "Principal", "Amazon Sales", -10m));

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
        var preview = Preview("Checking", new SettlementPreviewRow("ItemFees", "Commission", null, -5m));

        Assert.Throws<InvalidOperationException>(() => IifExportGenerator.Generate(preview));
    }

    [Fact]
    public void Generate_NoDepositAccount_Throws()
    {
        var preview = Preview(null, new SettlementPreviewRow("ItemPrice", "Principal", "Sales", 5m));

        Assert.Throws<InvalidOperationException>(() => IifExportGenerator.Generate(preview));
    }

    [Fact]
    public void Generate_NoLines_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => IifExportGenerator.Generate(Preview("Checking")));
    }
}
