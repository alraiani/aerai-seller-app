namespace AERai.Web.Application.Inventory;

/// <summary>Which SKUs an inventory overview lists, by <see cref="StockStatus"/>.</summary>
public enum StockStatusFilter
{
    /// <summary>Every SKU.</summary>
    All = 0,

    /// <summary>Restock overdue or due soon.</summary>
    NeedsAction = 1,

    /// <summary>Out of stock while selling.</summary>
    OutOfStock = 2,

    /// <summary>Selling with enough stock.</summary>
    Healthy = 3,

    /// <summary>Too little sales history for a rate.</summary>
    NoSalesData = 4,

    /// <summary>Held only at home.</summary>
    NotAtAmazon = 5,
}
