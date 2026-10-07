namespace AERai.Web.Application.Dashboard;

/// <summary>Inventory health in a few numbers.</summary>
/// <param name="SkusTotal">SKUs with an inventory snapshot.</param>
/// <param name="SkusInStock">SKUs with available units.</param>
/// <param name="AvailableUnits">Sellable units across all SKUs.</param>
/// <param name="InboundUnits">Units on the way to the fulfillment network.</param>
/// <param name="OutOfStockSelling">SKUs with no available units that sold in the last 30 days.</param>
/// <param name="LowStock">In-stock SKUs at or below the low-stock days-of-supply threshold.</param>
/// <param name="SnapshotDate">Most recent snapshot date, if any.</param>
public sealed record InventoryGlance(int SkusTotal, int SkusInStock, int AvailableUnits, int InboundUnits, int OutOfStockSelling, int LowStock, DateOnly? SnapshotDate);
