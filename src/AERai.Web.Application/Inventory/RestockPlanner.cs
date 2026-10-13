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
/// </remarks>
public static class RestockPlanner
{
    /// <summary>Plans a restock.</summary>
    /// <param name="today">Today in the marketplace's local calendar.</param>
    /// <param name="dailyVelocity">Units sold per day, or <see langword="null"/> when unknown.</param>
    /// <param name="sellThroughStock">Units at (or on the way to) Amazon that will sell without action.</param>
    /// <param name="homeStock">Units on hand outside Amazon.</param>
    /// <param name="leadTimes">The SKU's effective lead times.</param>
    /// <param name="amazon">Amazon's recommendation, if its restock report covers the SKU.</param>
    /// <param name="pendingToAmazon">Units sent from home that Amazon's numbers don't show yet.</param>
    /// <returns>The plan, or <see langword="null"/> when the sales rate is unknown or zero.</returns>
    public static RestockPlan? Plan(DateOnly today, decimal? dailyVelocity, int sellThroughStock, int homeStock, LeadTimes leadTimes, AmazonRecommendation? amazon = null, int pendingToAmazon = 0)
    {
        ArgumentNullException.ThrowIfNull(leadTimes);

        if (dailyVelocity is not { } perDay || perDay <= 0)
        {
            return null;
        }

        var pending = Math.Max(0, pendingToAmazon);
        var atAmazon = Math.Max(0, sellThroughStock) + pending;
        var atHome = Math.Max(0, homeStock);

        var stockout = today.AddDays(DaysOfStock(atAmazon, perDay));
        var needed = Math.Max(0, Units(perDay * leadTimes.TargetStockDays) - atAmazon);

        var amazonUnits = Math.Max(0, (amazon?.Quantity ?? 0) - pending);
        var wanted = Math.Max(needed, amazonUnits);
        var send = Math.Min(wanted, atHome);
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

        var allStockout = today.AddDays(DaysOfStock(atAmazon + atHome, perDay));
        var reorderBy = allStockout.AddDays(-leadTimes.OrderLeadDays);

        // Order up to the target cover as of arrival; when the order is already late, also make up
        // what will be sold before it lands.
        var reorder = Math.Max(
            Units(perDay * leadTimes.TargetStockDays),
            Units(perDay * (leadTimes.OrderLeadDays + leadTimes.TargetStockDays)) - (atAmazon + atHome));

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
            nextAction.DayNumber - today.DayNumber);
    }

    private static int DaysOfStock(int units, decimal perDay) => (int)Math.Floor(units / perDay);

    private static int Units(decimal value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
