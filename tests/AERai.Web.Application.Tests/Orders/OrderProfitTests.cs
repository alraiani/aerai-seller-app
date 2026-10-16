using AERai.Web.Application.Orders;

namespace AERai.Web.Application.Tests.Orders;

public sealed class OrderProfitTests
{
    private static OrderItemRow Item(string sku, int quantity, decimal price, decimal? unitCost) =>
        new("O-1", sku, null, null, null, null, quantity, price, unitCost);

    private static OrderPayout Payout(decimal sales, decimal fees, decimal refunds = 0, decimal other = 0) =>
        new(["S-1"], sales, fees, refunds, other);

    [Fact]
    public void Estimate_PaidOutWithEveryCost_IsNetLessCostOfGoods()
    {
        var profit = OrderProfit.Estimate("Shipped", [Item("A", 2, 40m, 6.50m), Item("B", 1, 15m, 3m)], Payout(55m, -12.40m));

        Assert.Equal(OrderProfitState.Estimated, profit.State);
        Assert.Equal(16m, profit.CostOfGoods);
        Assert.Equal(55m - 12.40m - 16m, profit.Profit);
    }

    [Fact]
    public void Estimate_NotPaidOut_HasNoProfitButKeepsTheCost()
    {
        var profit = OrderProfit.Estimate("Shipped", [Item("A", 2, 40m, 6.50m)], payout: null);

        Assert.Equal(OrderProfitState.NotPaidOut, profit.State);
        Assert.Equal(13m, profit.CostOfGoods);
        Assert.Null(profit.Profit);
    }

    [Fact]
    public void Estimate_AnItemWithoutCost_IsCostMissing()
    {
        var profit = OrderProfit.Estimate("Shipped", [Item("A", 1, 40m, 6.50m), Item("B", 1, 15m, null)], Payout(55m, -10m));

        Assert.Equal(OrderProfitState.CostMissing, profit.State);
        Assert.Null(profit.CostOfGoods);
        Assert.Null(profit.Profit);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Canceled")]
    [InlineData("CANCELLED")]
    public void Estimate_Cancelled_HasNoProfitEvenWhenPaidOut(string status)
    {
        var profit = OrderProfit.Estimate(status, [Item("A", 1, 40m, 6.50m)], Payout(0m, -1m));

        Assert.Equal(OrderProfitState.Cancelled, profit.State);
        Assert.Null(profit.Profit);
    }

    [Fact]
    public void Estimate_RefundedOrder_CanBeALoss()
    {
        var profit = OrderProfit.Estimate("Shipped", [Item("A", 1, 40m, 10m)], Payout(40m, -8m, refunds: -36m));

        Assert.Equal(-14m, profit.Profit);
    }

    [Fact]
    public void From_SeveralSettlements_AddsThemUpAndKeepsEachIdOnce()
    {
        var payout = OrderPayout.From(
        [
            new OrderSettlementAmounts("O-1", "S-1", "USD", 40m, -8m, 0m, 0m),
            new OrderSettlementAmounts("O-1", "S-2", "USD", 0m, 2m, -40m, 0m),
            new OrderSettlementAmounts("O-1", "S-2", "USD", 0m, 0m, 0m, 1m),
        ]);

        Assert.NotNull(payout);
        Assert.Equal(["S-1", "S-2"], payout.SettlementIds);
        Assert.Equal(-5m, payout.Net);
    }

    [Fact]
    public void From_NoAmounts_IsNull() => Assert.Null(OrderPayout.From([]));
}
