using AERai.Web.Application.Inventory;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class RestockPlannerTests
{
    private static readonly DateOnly Today = new(2026, 10, 5);

    // supplier 30 + prep 7 + transit 10 + safety 14; target 90 days.
    private static readonly LeadTimes Times = new(30, 7, 10, 14, 90, IsCustom: false);

    [Fact]
    public void Plan_UnknownOrZeroVelocity_NoPlan()
    {
        Assert.Null(RestockPlanner.Plan(Today, null, 100, 0, Times));
        Assert.Null(RestockPlanner.Plan(Today, 0m, 100, 0, Times));
    }

    [Fact]
    public void Plan_WorksBackFromStockoutThroughEveryStage()
    {
        // 2/day with 200 units: stockout in 100 days; target 180 units -> already covered.
        var covered = RestockPlanner.Plan(Today, 2m, 200, 0, Times)!;
        Assert.Equal(Today.AddDays(100), covered.ProjectedStockout);
        Assert.Equal(0, covered.UnitsNeeded);
        Assert.Null(covered.DaysUntilAction);

        // 2/day with 120 units: stockout in 60 days; need 180 - 120 = 60 from the supplier.
        var plan = RestockPlanner.Plan(Today, 2m, 120, 0, Times)!;
        Assert.Equal(60, plan.OrderFromSupplier);
        Assert.Equal(Today.AddDays(60 - 61), plan.OrderBy);   // 30 + 7 + 10 + 14 = 61 days of lead
        Assert.Equal(-1, plan.DaysUntilAction);               // a day overdue
    }

    [Fact]
    public void Plan_SendsHomeStockFirstAndOrdersOnlyTheShortfall()
    {
        // 1/day, 40 at Amazon, target 90 -> need 50; 30 at home.
        var plan = RestockPlanner.Plan(Today, 1m, 40, 30, Times)!;

        Assert.Equal((50, 30, 20), (plan.UnitsNeeded, plan.SendFromHome, plan.OrderFromSupplier));
        Assert.Equal(Today.AddDays(40 - 24), plan.SendBy);   // transit 10 + safety 14
        Assert.Equal(Today.AddDays(40 - 61), plan.OrderBy);
        Assert.Equal(-21, plan.DaysUntilAction);              // the supplier order is the earliest deadline
    }

    [Fact]
    public void Plan_HomeStockCoversEverything_ActionIsTheSendDate()
    {
        var plan = RestockPlanner.Plan(Today, 1m, 40, 500, Times)!;

        Assert.Equal((50, 50, 0), (plan.UnitsNeeded, plan.SendFromHome, plan.OrderFromSupplier));
        Assert.Equal(16, plan.DaysUntilAction);
    }

    [Fact]
    public void Plan_OutOfStock_StockoutIsToday()
    {
        var plan = RestockPlanner.Plan(Today, 3m, 0, 0, Times)!;

        Assert.Equal(Today, plan.ProjectedStockout);
        Assert.Equal(270, plan.OrderFromSupplier);
        Assert.Equal(-61, plan.DaysUntilAction);
    }
}
