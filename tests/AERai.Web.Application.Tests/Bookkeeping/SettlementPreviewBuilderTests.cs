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
        ],
        "USD",
        1m);

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

    private static SettlementBooks Cad(decimal? rate, decimal? manualHomeAmount = null) => new(
        "S2", MarketplaceIds.UnitedStates, "CAD", null, new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero),
        [
            new SettlementCategoryTotal("ItemPrice", "Principal", 100.01m, 4),
            new SettlementCategoryTotal("ItemFees", "Commission", -15.03m, 4),
            new SettlementCategoryTotal("ItemFees", "FBAPerUnitFulfillmentFee", -3.47m, 4),
        ],
        "USD",
        rate,
        "ECB 2026-09-16",
        manualHomeAmount);

    private static BookkeepingAccountMapping[] AllMapped =>
        [Mapping("ItemPrice", "Principal", "Sales"), Mapping("ItemFees", "Commission", "Fees"), Mapping("ItemFees", "FBAPerUnitFulfillmentFee", "Fees")];

    [Fact]
    public void Build_OtherCurrency_ConvertsIntoTheBooksCurrency()
    {
        var preview = SettlementPreviewBuilder.Build(Cad(0.72m), AllMapped, "Checking");

        Assert.Equal(("USD", 58.69m, "CAD", 81.51m, 0.72m), (preview.Currency, preview.NetTotal, preview.OriginalCurrency, preview.OriginalNetTotal, preview.ExchangeRate));
        Assert.Equal([("Fees", -13.32m), ("Sales", 72.01m)], preview.AccountTotals!.Select(a => (a.Account, a.Amount)));
        Assert.Equal(0m, preview.RoundingAdjustment);
        Assert.True(preview.CanExport);
    }

    [Fact]
    public void Build_AmountReceived_IsTheDepositExactly_AndTheCentGoesToTheLargestAccount()
    {
        var preview = SettlementPreviewBuilder.Build(Cad(0.72m, manualHomeAmount: 58.70m), AllMapped, "Checking");

        Assert.Equal(58.70m, preview.NetTotal);
        Assert.Equal(58.70m, preview.AccountTotals!.Sum(a => a.Amount));
        Assert.Equal(0.01m, preview.RoundingAdjustment);
        Assert.Equal(72.02m, preview.AccountTotals!.Single(a => a.Account == "Sales").Amount);
    }

    [Fact]
    public void Build_OtherCurrencyWithoutARate_CannotExport()
    {
        var preview = SettlementPreviewBuilder.Build(Cad(null), AllMapped, "Checking");

        Assert.True(preview.NeedsRate);
        Assert.False(preview.CanExport);
        Assert.Equal(("CAD", 81.51m), (preview.Currency, preview.NetTotal));
        Assert.All(preview.Rows, r => Assert.Null(r.HomeAmount));
    }
}
