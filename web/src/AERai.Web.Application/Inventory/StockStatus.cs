namespace AERai.Web.Application.Inventory;

/// <summary>
/// The one-word state of a SKU's stock, shared by the Inventory page, the dashboard, and stock
/// alerts so they always agree. Ordered from most to least urgent.
/// </summary>
public enum StockStatus
{
    /// <summary>Nothing available at Amazon while the SKU sold in the last 30 days.</summary>
    OutOfStock = 0,

    /// <summary>The last day to send or order has passed.</summary>
    RestockOverdue = 1,

    /// <summary>The next send or order is due within the alert window.</summary>
    RestockSoon = 2,

    /// <summary>Selling with enough stock (or a restock that isn't due yet).</summary>
    Healthy = 3,

    /// <summary>At Amazon, but too little sales history to estimate a sales rate.</summary>
    NoSalesData = 4,

    /// <summary>Held only at home; nothing at Amazon yet.</summary>
    NotAtAmazon = 5,
}
