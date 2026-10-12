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
    public void Plan_EnoughAtAmazon_NothingToSend()
    {
        // 2/day with 200 units: stockout in 100 days; target 180 units -> already covered.
        var plan = RestockPlanner.Plan(Today, 2m, 200, 50, Times)!;

        Assert.Equal(Today.AddDays(100), plan.ProjectedStockout);
        Assert.Equal((0, 0, 0), (plan.UnitsNeeded, plan.SendToAmazon, plan.ShortAtHome));
        Assert.Null(plan.SendBy);
    }

    [Fact]
    public void Plan_SendsWhatAmazonNeedsFromHomeByTheTransitDeadline()
    {
        // 1/day, 40 at Amazon, target 90 -> need 50; plenty at home.
        var plan = RestockPlanner.Plan(Today, 1m, 40, 500, Times)!;

        Assert.Equal((50, 50, 0), (plan.UnitsNeeded, plan.SendToAmazon, plan.ShortAtHome));
        Assert.Equal(Today.AddDays(40 - 24), plan.SendBy);   // transit 10 + safety 14
        Assert.Equal(16, plan.DaysUntilAction);
    }

    [Fact]
    public void Plan_HomeStockShort_SendsAllOfItAndReportsTheShortfall()
    {
        var plan = RestockPlanner.Plan(Today, 1m, 40, 30, Times)!;

        Assert.Equal((50, 30, 20), (plan.UnitsNeeded, plan.SendToAmazon, plan.ShortAtHome));
    }

    [Fact]
    public void Plan_AmazonAsksForMoreAndSooner_FollowsAmazon()
    {
        var amazon = new AmazonRecommendation(80, Today.AddDays(5));

        var plan = RestockPlanner.Plan(Today, 1m, 40, 500, Times, amazon)!;

        Assert.Equal(80, plan.SendToAmazon);
        Assert.Equal(Today.AddDays(5), plan.SendBy);
        Assert.Same(amazon, plan.Amazon);
    }

    [Fact]
    public void Plan_AmazonAsksForLessAndLater_KeepsOurNumbers()
    {
        var plan = RestockPlanner.Plan(Today, 1m, 40, 500, Times, new AmazonRecommendation(10, Today.AddDays(30)))!;

        Assert.Equal(50, plan.SendToAmazon);
        Assert.Equal(Today.AddDays(16), plan.SendBy);
    }

    [Fact]
    public void Plan_OnlyAmazonWantsUnits_SendsByAmazonsDate()
    {
        // Covered by our math, but Amazon recommends 25 by the 12th.
        var plan = RestockPlanner.Plan(Today, 2m, 200, 100, Times, new AmazonRecommendation(25, Today.AddDays(7)))!;

        Assert.Equal((0, 25), (plan.UnitsNeeded, plan.SendToAmazon));
        Assert.Equal(Today.AddDays(7), plan.SendBy);
    }

    [Fact]
    public void Plan_AmazonGivesNoDate_DueToday()
    {
        var plan = RestockPlanner.Plan(Today, 2m, 200, 100, Times, new AmazonRecommendation(25, null))!;

        Assert.Equal(Today, plan.SendBy);
        Assert.Equal(0, plan.DaysUntilAction);
    }

    [Fact]
    public void Plan_ReorderWorksBackFromWhenAllStockRunsOut()
    {
        // 1/day, 40 at Amazon + 160 at home = 200 days; lead 30 + 7 + 10 + 14 = 61.
        var plan = RestockPlanner.Plan(Today, 1m, 40, 160, Times)!;

        Assert.Equal(Today.AddDays(200 - 61), plan.ReorderBy);
        Assert.Equal(139, plan.DaysUntilReorder);
        Assert.Equal(90, plan.ReorderQuantity);               // a target's worth once it lands
        Assert.Equal(16, plan.DaysUntilAction);               // the send comes first
    }

    [Fact]
    public void Plan_ReorderLate_OrdersEnoughToCatchUp()
    {
        // 3/day, nothing anywhere: overdue by the whole lead time; cover lead + target.
        var plan = RestockPlanner.Plan(Today, 3m, 0, 0, Times)!;

        Assert.Equal(Today, plan.ProjectedStockout);
        Assert.Equal(-61, plan.DaysUntilReorder);
        Assert.Equal(3 * (61 + 90), plan.ReorderQuantity);
        Assert.Equal(-61, plan.DaysUntilAction);
        Assert.Equal(270, plan.ShortAtHome);
    }

    [Fact]
    public void Plan_FullSendPendingAtAmazon_SuggestsNothingMore()
    {
        // 1/day, 40 at Amazon, target 90 -> send 50; then all 50 are sent but Amazon doesn't show them yet.
        var before = RestockPlanner.Plan(Today, 1m, 40, 500, Times)!;
        var after = RestockPlanner.Plan(Today, 1m, 40, 450, Times, pendingToAmazon: 50)!;

        Assert.Equal(50, before.SendToAmazon);
        Assert.Equal((0, 0, 0), (after.UnitsNeeded, after.SendToAmazon, after.ShortAtHome));
        Assert.Null(after.SendBy);
        Assert.Equal(Today.AddDays(90), after.ProjectedStockout);
    }

    [Fact]
    public void Plan_PartialSendPending_SuggestsTheRemainder()
    {
        var plan = RestockPlanner.Plan(Today, 1m, 40, 480, Times, pendingToAmazon: 20)!;

        Assert.Equal((30, 30, 0), (plan.UnitsNeeded, plan.SendToAmazon, plan.ShortAtHome));
    }

    [Fact]
    public void Plan_PendingComesOffAmazonsRecommendation()
    {
        // Amazon asked for 80 before the send; 80 sent and pending -> nothing left to send.
        var amazon = new AmazonRecommendation(80, Today.AddDays(5));
        var all = RestockPlanner.Plan(Today, 1m, 40, 420, Times, amazon, pendingToAmazon: 80)!;
        var part = RestockPlanner.Plan(Today, 2m, 400, 470, Times, amazon, pendingToAmazon: 30)!;

        Assert.Equal((0, null), (all.SendToAmazon, all.SendBy));
        Assert.Equal((50, Today.AddDays(5)), (part.SendToAmazon, part.SendBy)); // Our own need is covered; Amazon's 80 − 30 remains.
    }

    [Fact]
    public void Plan_SendingDoesNotMoveTheReorder()
    {
        // Units moving from home to "pending" are still stock the seller owns.
        var before = RestockPlanner.Plan(Today, 3m, 100, 200, Times)!;
        var after = RestockPlanner.Plan(Today, 3m, 100, 120, Times, pendingToAmazon: 80)!;

        Assert.Equal((before.ReorderBy, before.ReorderQuantity), (after.ReorderBy, after.ReorderQuantity));
    }
}
