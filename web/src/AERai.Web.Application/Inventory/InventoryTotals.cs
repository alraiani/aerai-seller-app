namespace AERai.Web.Application.Inventory;

/// <summary>Unit totals across a set of SKUs in one marketplace.</summary>
/// <param name="SkuCount">SKUs included.</param>
/// <param name="Available">Sellable units.</param>
/// <param name="Inbound">Units on the way to the fulfillment network.</param>
/// <param name="Reserved">Reserved units (customer orders, FC transfers, FC processing).</param>
/// <param name="Unfulfillable">Units that cannot be sold.</param>
/// <param name="SnapshotDate">The newest snapshot date among the SKUs, or <see langword="null"/> when there are none.</param>
public sealed record InventoryTotals(int SkuCount, int Available, int Inbound, int Reserved, int Unfulfillable, DateOnly? SnapshotDate)
{
    /// <summary>Units physically in Amazon's fulfillment centers: available, reserved, and unfulfillable.</summary>
    public int InWarehouse => Available + Reserved + Unfulfillable;

    /// <summary>Every unit Amazon reports, in any state.</summary>
    public int AmazonTotal => InWarehouse + Inbound;
}
