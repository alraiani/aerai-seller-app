namespace AERai.Web.Application.Inventory;

/// <summary>
/// Works out a SKU's restock plan from its sales rate, stock, lead times, and Amazon's own
/// recommendation. Pure and stateless.
/// </summary>
/// <remarks>
/// The day-to-day decision is how much home stock to send into Amazon, so that comes first: our
/// math tops Amazon's stock up to the target days of sales (deadline = stockout − transit − safety),
/// and Amazon's recommended quantity and ship date are folded in so neither source's advice is
/// missed. Supplier orders are planned separately from <em>all</em> stock (Amazon + home): the
/// reorder must land before everything runs out, so its deadline works back through supplier, prep,
/// transit, and safety, and it orders enough to cover the target days once it arrives.
/// Units already sent from home that Amazon's numbers don't show yet ("pending to Amazon") count as
/// Amazon stock, and come off Amazon's recommendation too (it was made before the send), so a send
/// isn't suggested again while Amazon and our sync catch up.
/// Demand comes from a <see cref="DemandForecast"/>, so cover is counted in the sales expected on those days
/// (busier ahead of wedding season), and the family's <see cref="RestockRules"/> round the send and the order
/// to how they are really packed.
/// </remarks>
public static class RestockPlanner
{
    /// <summary>Plans a restock at a flat sales rate, with no rounding rules.</summary>
    /// <param name="today">Today in the marketplace's local calendar.</param>
    /// <param name="dailyVelocity">Units sold per day, or <see langword="null"/> when unknown.</param>
    /// <param name="sellThroughStock">Units at (or on the way to) Amazon that will sell without action.</param>
    /// <param name="homeStock">Units on hand outside Amazon.</param>
    /// <param name="leadTimes">The SKU's effective lead times.</param>
    /// <param name="amazon">Amazon's recommendation, if its restock report covers the SKU.</param>
    /// <param name="pendingToAmazon">Units sent from home that Amazon's numbers don't show yet.</param>
    /// <returns>The plan, or <see langword="null"/> when the sales rate is unknown or zero.</returns>
    public static RestockPlan? Plan(DateOnly today, decimal? dailyVelocity, int sellThroughStock, int homeStock, LeadTimes leadTimes, AmazonRecommendation? amazon = null, int pendingToAmazon = 0) =>
        dailyVelocity is { } perDay && perDay > 0
            ? Plan(today, DemandForecast.Flat(perDay), sellThroughStock, homeStock, leadTimes, RestockRules.None, amazon, pendingToAmazon)
            : null;

    /// <summary>Plans a restock from a demand forecast (seasonal or flat) and the family's rounding rules.</summary>
    /// <param name="today">Today in the marketplace's local calendar.</param>
    /// <param name="demand">Expected sales per day ahead, or <see langword="null"/> when unknown.</param>
    /// <param name="sellThroughStock">Units at (or on the way to) Amazon that will sell without action.</param>
    /// <param name="homeStock">Units on hand outside Amazon.</param>
    /// <param name="leadTimes">The SKU's effective lead times.</param>
    /// <param name="rules">How sends and supplier orders are rounded.</param>
    /// <param name="amazon">Amazon's recommendation, if its restock report covers the SKU.</param>
    /// <param name="pendingToAmazon">Units sent from home that Amazon's numbers don't show yet.</param>
    /// <param name="limits">Aging limits; <see cref="StockLimits.None"/> to send whatever the plan wants.</param>
    /// <returns>The plan, or <see langword="null"/> when the sales rate is unknown or zero.</returns>
    public static RestockPlan? Plan(
        DateOnly today,
        DemandForecast? demand,
        int sellThroughStock,
        int homeStock,
        LeadTimes leadTimes,
        RestockRules rules,
        AmazonRecommendation? amazon = null,
        int pendingToAmazon = 0,
        StockLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(leadTimes);
        ArgumentNullException.ThrowIfNull(rules);

        if (demand is not { BaseRate: > 0 } forecast)
        {
            return null;
        }

        var pending = Math.Max(0, pendingToAmazon);
        var atAmazon = Math.Max(0, sellThroughStock) + pending;
        var atHome = Math.Max(0, homeStock);

        // Cover is measured in expected sales, so ahead of wedding season the same days need more units.
        var stockout = today.AddDays(forecast.DaysCovered(today, atAmazon));
        var needed = Math.Max(0, Units(forecast.Units(today, leadTimes.TargetStockDays)) - atAmazon);

        var amazonUnits = Math.Max(0, (amazon?.Quantity ?? 0) - pending);
        var limit = limits ?? StockLimits.None;

        // Amazon's own excess estimate wins: sending more of a SKU it already can't sell in time only buys fees.
        var heldForExcess = limit.ExcessAtAmazon > 0 && Math.Max(needed, amazonUnits) > 0;
        var room = limit.MaxAmazonCoverDays > 0
            ? Math.Max(0, (int)Math.Floor(forecast.Units(today, limit.MaxAmazonCoverDays)) - atAmazon)
            : int.MaxValue;
        var target = heldForExcess ? 0 : Math.Max(needed, amazonUnits);
        var cappedForAge = target > room;
        var (send, wanted) = rules.RoundSend(Math.Min(target, room), Math.Min(atHome, room));
        DateOnly? sendBy = null;
        if (send > 0)
        {
            var ours = needed > 0 ? stockout.AddDays(-leadTimes.SendLeadDays) : (DateOnly?)null;
            var theirs = amazonUnits > 0 ? amazon?.ShipDate : null;
            sendBy = (ours, theirs) switch
            {
                ({ } a, { } b) => a < b ? a : b,
                ({ } a, null) => a,
                (null, { } b) => b,
                // Amazon wants units but gave no date: treat it as due now.
                _ => today,
            };
        }

        var allStockout = today.AddDays(forecast.DaysCovered(today, atAmazon + atHome));
        var reorderBy = allStockout.AddDays(-leadTimes.OrderLeadDays);

        // Order up to the target cover as of arrival; when the order is already late, also make up
        // what will be sold before it lands.
        var reorder = rules.RoundOrder(Math.Max(
            Units(forecast.Units(today.AddDays(leadTimes.OrderLeadDays), leadTimes.TargetStockDays)),
            Units(forecast.Units(today, leadTimes.OrderLeadDays + leadTimes.TargetStockDays)) - (atAmazon + atHome)));

        var nextAction = sendBy is { } s && s < reorderBy ? s : reorderBy;
        return new RestockPlan(
            stockout,
            needed,
            send,
            sendBy,
            wanted - send,
            amazon,
            reorder,
            reorderBy,
            reorderBy.DayNumber - today.DayNumber,
            sendBy?.DayNumber - today.DayNumber,
            nextAction.DayNumber - today.DayNumber,
            forecast.Season.IsLearned ? Math.Round(forecast.Season.Average(today, leadTimes.TargetStockDays), 2) : null,
            heldForExcess,
            cappedForAge);
    }

    private static int Units(decimal value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
