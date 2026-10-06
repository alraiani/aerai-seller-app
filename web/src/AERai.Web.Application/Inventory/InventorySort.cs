namespace AERai.Web.Application.Inventory;

/// <summary>Order of SKUs in an inventory overview.</summary>
public enum InventorySort
{
    /// <summary>Most urgent first: status, then soonest restock action, then fewest days of inventory.</summary>
    Urgency = 0,

    /// <summary>By SKU, for scanning a list while counting stock.</summary>
    Sku = 1,

    /// <summary>Fewest days of inventory first (unknown last).</summary>
    DaysOfInventory = 2,

    /// <summary>Best sellers over the last 30 days first.</summary>
    Sold30d = 3,

    /// <summary>Most sellable units first.</summary>
    Available = 4,
}
