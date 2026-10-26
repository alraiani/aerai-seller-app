using AERai.Web.Application.Settlements;

namespace AERai.Web.Application.Tests.Settlements;

public sealed class SettlementOtherExpensesTests
{
    private static SettlementExpenseLine Line(string? sku, string type, string? amountType, string? description, decimal amount, decimal? home = null) =>
        new(sku, sku is null ? null : 10m, type, amountType, description, amount, home);

    [Theory]
    [InlineData("ServiceFee", "Cost of Advertising", "TransactionTotalAmount", "Advertising")]
    [InlineData("ServiceFee", "Refund for Advertiser", "TransactionTotalAmount", "Advertising")]
    [InlineData("Order", "Promotion", "Principal", "Promotions")]
    [InlineData("other-transaction", "FBA Inventory Reimbursement", "COMPENSATED_CLAWBACK", "Reimbursement clawbacks")]
    [InlineData("AmazonFees", "Deal Participation Fee", "Base fee", "Deal Participation Fee")]
    [InlineData("other-transaction", "other-transaction", "Subscription Fee", "Subscription Fee")]
    [InlineData("ServiceFee", null, null, "ServiceFee")]
    public void LabelOf_NamesTheChargeInWords(string type, string? amountType, string? description, string expected) =>
        Assert.Equal(expected, SettlementOtherExpenses.LabelOf(type, amountType, description));

    [Fact]
    public void Items_MergesEqualLabelsAndListsTheLargestChargeFirst()
    {
        var items = SettlementOtherExpenses.Items(
        [
            Line(null, "ServiceFee", "other-transaction", "Subscription Fee", -39.99m),
            Line(null, "ServiceFee", "Cost of Advertising", "TransactionTotalAmount", -100m),
            Line(null, "ServiceFee", "Refund for Advertiser", "TransactionTotalAmount", 5m),
            Line(null, "other-transaction", "other-transaction", "Successful charge", 12m),
            Line(null, "Order", "ItemFee", "Variable closing fee", 0m),
        ], SettlementAmounts.Home);

        Assert.Equal([("Advertising", -95m), ("Subscription Fee", -39.99m), ("Successful charge", 12m)], items.Select(i => (i.Label, i.Amount)));
    }

    [Fact]
    public void Items_ManyCharges_AddsUpThoseBeyondTheLimit()
    {
        var lines = Enumerable.Range(1, SettlementOtherExpenses.MaxItems + 2)
            .Select(i => Line(null, "ServiceFee", "other-transaction", $"Fee {i}", -i))
            .ToList();

        var items = SettlementOtherExpenses.Items(lines, SettlementAmounts.Home);

        Assert.Equal(SettlementOtherExpenses.MaxItems, items.Count);
        Assert.Equal((SettlementOtherExpenses.RestLabel, -6m), (items[^1].Label, items[^1].Amount)); // fees 1, 2, and 3
        Assert.Equal(lines.Sum(l => l.Amount), items.Sum(i => i.Amount));
    }

    [Fact]
    public void Items_Home_UsesConvertedAmountsWhereThereIsARate()
    {
        var line = Line(null, "ServiceFee", "other-transaction", "Subscription Fee", -40m, home: -29m);

        Assert.Equal(-29m, Assert.Single(SettlementOtherExpenses.Items([line], SettlementAmounts.Home)).Amount);
        Assert.Equal(-40m, Assert.Single(SettlementOtherExpenses.Items([line], SettlementAmounts.Original)).Amount);
        Assert.Equal(-40m, Assert.Single(SettlementOtherExpenses.Items([line with { HomeAmount = null }], SettlementAmounts.Home)).Amount);
    }

    [Fact]
    public void ByRow_KeysEachRowBySkuAndPrice()
    {
        var rows = SettlementOtherExpenses.ByRow(
        [
            Line("SKU-1", "Order", "Promotion", "Principal", -1m),
            Line(null, "ServiceFee", "other-transaction", "Subscription Fee", -39.99m),
        ], SettlementAmounts.Home);

        Assert.Equal("Promotions", Assert.Single(rows[("SKU-1", 10m)]).Label);
        Assert.Equal("Subscription Fee", Assert.Single(rows[(null, null)]).Label);
    }
}
