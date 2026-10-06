namespace AERai.Web.Application.Inventory;

/// <summary>
/// Works out a SKU's restock plan from its sales rate, stock, and lead times. Pure and stateless.
/// </summary>
/// <remarks>
/// Ported from the desktop app's replenishment planning so both apps agree: the projected stockout
/// is today plus days of inventory; the order-by date works back from it through safety, transit,
/// prep, and supplier lead time; the quantity tops stock up to the target days of sales. The web
/// app adds home stock: units already on hand are sent first (they only need transit, so their
/// deadline is later), and only the shortfall is ordered from the supplier.
/// </remarks>
public static class RestockPlanner
{
    /// <summary>Plans a restock.</summary>
    /// <param name="today">Today in the marketplace's local calendar.</param>
    /// <param name="dailyVelocity">Units sold per day, or <see langword="null"/> when unknown.</param>
    /// <param name="sellThroughStock">Units at (or on the way to) Amazon that will sell without action.</param>
    /// <param name="homeStock">Units on hand outside Amazon.</param>
    /// <param name="leadTimes">The SKU's effective lead times.</param>
    /// <returns>The plan, or <see langword="null"/> when the sales rate is unknown or zero.</returns>
    public static RestockPlan? Plan(DateOnly today, decimal? dailyVelocity, int sellThroughStock, int homeStock, LeadTimes leadTimes)
    {
        ArgumentNullException.ThrowIfNull(leadTimes);

        if (dailyVelocity is not { } perDay || perDay <= 0)
        {
            return null;
        }

        var stockout = today.AddDays((int)Math.Floor(Math.Max(0, sellThroughStock) / perDay));
        var needed = Math.Max(0, (int)Math.Round(perDay * leadTimes.TargetStockDays, MidpointRounding.AwayFromZero) - sellThroughStock);
        var fromHome = Math.Min(needed, Math.Max(0, homeStock));
        var fromSupplier = needed - fromHome;

        var sendBy = stockout.AddDays(-leadTimes.SendLeadDays);
        var orderBy = stockout.AddDays(-leadTimes.OrderLeadDays);

        DateOnly? nextAction = fromSupplier > 0 ? orderBy : fromHome > 0 ? sendBy : null;
        return new RestockPlan(
            stockout,
            needed,
            fromHome,
            sendBy,
            fromSupplier,
            orderBy,
            nextAction is { } action ? action.DayNumber - today.DayNumber : null);
    }
}
