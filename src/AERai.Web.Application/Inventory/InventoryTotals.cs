namespace AERai.Web.Application.Inventory;

/// <summary>Unit totals across a set of SKUs in one marketplace.</summary>
/// <param name="SkuCount">SKUs included.</param>
/// <param name="Available">Sellable units.</param>
/// <param name="Inbound">Units on the way to the fulfillment network.</param>
/// <param name="Reserved">Reserved units (customer orders, FC transfers, FC processing).</param>
/// <param name="Unfulfillable">Units that cannot be sold.</param>
/// <param name="HomeStock">Units held outside Amazon.</param>
/// <param name="AwdOnHand">Units in Amazon Warehousing and Distribution (AWD).</param>
/// <param name="AwdInbound">Units on their way into AWD.</param>
/// <param name="OutOfStock">SKUs out of stock while selling.</param>
/// <param name="RestockOverdue">SKUs whose restock deadline has passed.</param>
/// <param name="RestockSoon">SKUs whose restock is due within the alert window.</param>
/// <param name="SnapshotDate">The newest snapshot date among the SKUs, or <see langword="null"/> when there are none.</param>
public sealed record InventoryTotals(int SkuCount, int Available, int Inbound, int Reserved, int Unfulfillable, int HomeStock, int AwdOnHand, int AwdInbound, int OutOfStock, int RestockOverdue, int RestockSoon, DateOnly? SnapshotDate)
{
    /// <summary>Units physically in Amazon's fulfillment centers: available, reserved, and unfulfillable.</summary>
    public int InWarehouse => Available + Reserved + Unfulfillable;

    /// <summary>Every unit Amazon reports in the FBA network, in any state.</summary>
    public int AmazonTotal => InWarehouse + Inbound;

    /// <summary>Units in AWD or on their way into it.</summary>
    public int AwdTotal => AwdOnHand + AwdInbound;

    /// <summary>SKUs with a restock overdue or due soon.</summary>
    public int NeedsAction => RestockOverdue + RestockSoon;

    /// <summary>Every unit the seller owns: FBA, AWD, and home stock.</summary>
    public int OverallTotal => AmazonTotal + AwdTotal + HomeStock;
}
