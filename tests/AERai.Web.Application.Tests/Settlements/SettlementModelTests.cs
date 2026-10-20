using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Tests.Settlements;

public sealed class SettlementModelTests
{
    [Fact]
    public void FeeRate_WithSales_IsFeesAsAPositiveShareOfSales()
    {
        var totals = new SettlementTotals("USD", 2, AwaitingRate: 0, Sales: 200m, Expenses: -30m, Refunds: -10m, Reserve: 0m, Net: 160m);

        Assert.Equal(0.15m, totals.ExpenseRate);
        Assert.Equal(0.05m, totals.RefundRate);
    }

    [Fact]
    public void FeeRate_WithoutSales_IsUnknown()
    {
        var totals = new SettlementTotals("USD", 1, AwaitingRate: 0, Sales: 0m, Expenses: -39.99m, Refunds: 0m, Reserve: 0m, Net: -39.99m);

        Assert.Null(totals.ExpenseRate);
        Assert.Null(totals.RefundRate);
    }

    [Theory]
    [InlineData(SettlementSort.PeriodEnd, true)]
    [InlineData(SettlementSort.Sales, true)]
    [InlineData(SettlementSort.Net, true)]
    [InlineData(SettlementSort.Expenses, false)]
    [InlineData(SettlementSort.Refunds, false)]
    public void IsDescendingByDefault_SettlementSort_PutsTheLargestAmountFirst(SettlementSort sort, bool expected) =>
        Assert.Equal(expected, sort.IsDescendingByDefault());

    [Theory]
    [InlineData(SettlementSkuSort.Sales, true)]
    [InlineData(SettlementSkuSort.Units, true)]
    [InlineData(SettlementSkuSort.Orders, true)]
    [InlineData(SettlementSkuSort.Net, true)]
    [InlineData(SettlementSkuSort.Sku, false)]
    [InlineData(SettlementSkuSort.Expenses, false)]
    [InlineData(SettlementSkuSort.Refunds, false)]
    public void IsDescendingByDefault_SkuSort_PutsTheLargestAmountFirst(SettlementSkuSort sort, bool expected) =>
        Assert.Equal(expected, sort.IsDescendingByDefault());

    [Fact]
    public void TransactionTypes_ListsEachTypeOnceInFirstSeenOrder()
    {
        var group = new SettlementOrderGroup("O-1", null, null, 1, -1.5m, 10m, -1.5m, -10m, 0m, "USD",
            [Line("Order"), Line("Order"), Line("Refund"), Line("order")]);

        Assert.True(group.IsOrder);
        Assert.Equal(["Order", "Refund"], group.TransactionTypes);
    }

    [Fact]
    public void IsOrder_GroupWithoutAnOrder_IsFalse()
    {
        var group = new SettlementOrderGroup(null, "ServiceFee", null, 0, -39.99m, 0m, 0m, 0m, -39.99m, "USD", [Line("ServiceFee")]);

        Assert.False(group.IsOrder);
    }

    private static SettlementLineDetail Line(string transactionType) =>
        new() { MarketplaceId = "M", SettlementId = "S", TransactionType = transactionType, HomeCurrency = "USD" };
}
