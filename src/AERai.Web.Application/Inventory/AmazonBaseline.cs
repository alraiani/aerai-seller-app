namespace AERai.Web.Application.Inventory;

/// <summary>
/// A SKU's FBA stock at the moment a send to Amazon is logged: what later snapshots are compared with to
/// see the sent units arrive (see <see cref="PendingReconciliation"/>).
/// </summary>
/// <param name="Units">FBA sell-through stock (available, inbound, FC transfers and processing).</param>
/// <param name="ReceivedAt">When that snapshot was received, or <see langword="null"/> when the SKU had none.</param>
public sealed record AmazonBaseline(int Units, DateTimeOffset? ReceivedAt)
{
    /// <summary>Takes the baseline from a SKU's current position.</summary>
    /// <param name="position">The SKU's position, or <see langword="null"/> when it has none (nothing at Amazon).</param>
    /// <returns>The baseline.</returns>
    public static AmazonBaseline Of(Domain.Reporting.InventoryPosition? position) =>
        new(position?.SellThroughStock ?? 0, position?.SnapshotReceivedAt);
}
