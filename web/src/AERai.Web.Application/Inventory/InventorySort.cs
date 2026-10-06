namespace AERai.Web.Application.Inventory;

/// <summary>Order of SKUs in an inventory overview.</summary>
public enum InventorySort
{
    /// <summary>Soonest restock action first, then fewest days of inventory; unknown sales rate last.</summary>
    Urgency = 0,

    /// <summary>By SKU, for scanning a list while counting stock.</summary>
    Sku = 1,
}
