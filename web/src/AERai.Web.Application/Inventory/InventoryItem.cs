using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Inventory;

/// <summary>One SKU's stock in a marketplace with its recent sales and how long its stock will last.</summary>
/// <param name="Position">Current stock by state.</param>
/// <param name="UnitsSold30d">Units sold in the last 30 local days, including today.</param>
/// <param name="UnitsSold90d">Units sold in the last 90 local days, including today.</param>
/// <param name="DailyVelocity">
/// Average units sold per day, or <see langword="null"/> when there is too little sales history to
/// estimate it (see <see cref="InventoryService"/>).
/// </param>
/// <param name="DaysOfInventory">
/// How many days <see cref="InventoryPosition.SellThroughStock"/> lasts at <paramref name="DailyVelocity"/>,
/// or <see langword="null"/> when velocity is unknown.
/// </param>
/// <param name="LeadTimes">The SKU's effective lead times in this marketplace.</param>
/// <param name="Restock">What to send or order and by when, or <see langword="null"/> when velocity is unknown.</param>
/// <param name="Status">The SKU's stock state (see <see cref="StockStatus"/>).</param>
/// <param name="AveragePerDay30d">
/// Average units per day over the last 30 days, counting only the days since the SKU's first sale in
/// that window (so a short sales history isn't diluted); <see langword="null"/> when nothing sold.
/// </param>
/// <param name="AveragePerDay90d">The same over the last 90 days.</param>
public sealed record InventoryItem(
    InventoryPosition Position,
    int UnitsSold30d,
    int UnitsSold90d,
    decimal? DailyVelocity,
    decimal? DaysOfInventory,
    LeadTimes LeadTimes,
    RestockPlan? Restock,
    StockStatus Status,
    decimal? AveragePerDay30d,
    decimal? AveragePerDay90d)
{
    /// <summary>Seller SKU.</summary>
    public string Sku => Position.Sku;

    /// <summary>Whether the SKU sold anything in the last 30 days.</summary>
    public bool IsSelling => UnitsSold30d > 0;
}
