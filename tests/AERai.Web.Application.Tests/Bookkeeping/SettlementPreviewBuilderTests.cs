using AERai.Web.Application.Bookkeeping;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Bookkeeping;

public sealed class SettlementPreviewBuilderTests
{
    private static readonly SettlementBooks Settlement = new(
        "S1", MarketplaceIds.UnitedStates, "USD", null, null,
        [
            new SettlementCategoryTotal("ItemPrice", "Principal", 100m, 4),
            new SettlementCategoryTotal("ItemFees", "Commission", -15m, 4),
            new SettlementCategoryTotal("ItemFees", "FBAPerUnitFulfillmentFee", -3.5m, 4),
        ]);

    private static BookkeepingAccountMapping Mapping(string type, string description, string account) =>
        new() { AmountType = type, AmountDescription = description, QuickBooksAccountName = account, UpdatedBy = "t" };

    [Fact]
    public void Build_JoinsMappingsAndReportsWhatIsUnmapped()
    {
        var preview = SettlementPreviewBuilder.Build(
            Settlement, [Mapping("ItemPrice", "Principal", "Sales"), Mapping("ItemFees", "Commission", "Fees")], "Checking");

        Assert.Equal(81.5m, preview.NetTotal);
        Assert.Equal(["ItemFees/Commission", "ItemFees/FBAPerUnitFulfillmentFee", "ItemPrice/Principal"], preview.Rows.Select(r => $"{r.AmountType}/{r.AmountDescription}"));
        var unmapped = Assert.Single(preview.Unmapped);
        Assert.Equal("FBAPerUnitFulfillmentFee", unmapped.AmountDescription);
        Assert.True(preview.HasUnmapped);
        Assert.False(preview.CanExport);
    }

    [Fact]
    public void Build_EverythingMappedWithADepositAccount_CanExport()
    {
        var preview = SettlementPreviewBuilder.Build(
            Settlement,
            [Mapping("ItemPrice", "Principal", "Sales"), Mapping("ItemFees", "Commission", "Fees"), Mapping("ItemFees", "FBAPerUnitFulfillmentFee", "Fees")],
            "Checking");

        Assert.True(preview.CanExport);
        Assert.Equal("Checking", preview.DepositAccountName);
    }

    [Fact]
    public void Build_BlankDepositAccount_CannotExport()
    {
        var preview = SettlementPreviewBuilder.Build(
            Settlement,
            [Mapping("ItemPrice", "Principal", "Sales"), Mapping("ItemFees", "Commission", "Fees"), Mapping("ItemFees", "FBAPerUnitFulfillmentFee", "Fees")],
            "  ");

        Assert.False(preview.HasDepositAccount);
        Assert.False(preview.CanExport);
    }
}
