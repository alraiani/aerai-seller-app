using AERai.Seller.Domain;

namespace AERai.Seller.Application.Reports;

/// <summary>
/// Maps parsed GET_FBA_INVENTORY_PLANNING_DATA rows into per-state InventorySnapshot entities.
/// Column names below match Amazon's documented flat-file report as of this writing — verify against
/// a real report pulled via the "Reports" folder in the AERai Seller App Postman collection before
/// relying on this in production; report schemas do occasionally change column names/casing.
/// </summary>
public static class InventoryPlanningReportParser
{
    public static IReadOnlyList<InventorySnapshot> Parse(
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows, DateOnly snapshotDate, DateTimeOffset syncedAt)
    {
        var snapshots = new List<InventorySnapshot>();

        foreach (var row in rows)
        {
            if (!row.TryGetValue("sku", out var sku) || string.IsNullOrWhiteSpace(sku))
            {
                continue;
            }

            AddIfPresent(snapshots, row, sku, "afn-fulfillable-quantity", InventoryState.Available, snapshotDate, syncedAt);
            AddIfPresent(snapshots, row, sku, "afn-reserved-quantity", InventoryState.Reserved, snapshotDate, syncedAt);
            AddIfPresent(snapshots, row, sku, "afn-unsellable-quantity", InventoryState.Unfulfillable, snapshotDate, syncedAt);
            AddIfPresent(snapshots, row, sku, "afn-researching-quantity", InventoryState.Researching, snapshotDate, syncedAt);

            var inbound = SumIfPresent(row, "afn-inbound-working-quantity", "afn-inbound-shipped-quantity", "afn-inbound-receiving-quantity");
            if (inbound is > 0)
            {
                snapshots.Add(new InventorySnapshot
                {
                    Sku = sku,
                    State = InventoryState.Inbound,
                    Quantity = inbound.Value,
                    SnapshotDate = snapshotDate,
                    SyncedAt = syncedAt,
                });
            }
        }

        return snapshots;
    }

    private static void AddIfPresent(
        List<InventorySnapshot> snapshots,
        IReadOnlyDictionary<string, string> row,
        string sku,
        string columnName,
        InventoryState state,
        DateOnly snapshotDate,
        DateTimeOffset syncedAt)
    {
        if (row.TryGetValue(columnName, out var raw) && int.TryParse(raw, out var quantity))
        {
            snapshots.Add(new InventorySnapshot
            {
                Sku = sku,
                State = state,
                Quantity = quantity,
                SnapshotDate = snapshotDate,
                SyncedAt = syncedAt,
            });
        }
    }

    private static int? SumIfPresent(IReadOnlyDictionary<string, string> row, params string[] columnNames)
    {
        int? sum = null;
        foreach (var columnName in columnNames)
        {
            if (row.TryGetValue(columnName, out var raw) && int.TryParse(raw, out var quantity))
            {
                sum = (sum ?? 0) + quantity;
            }
        }
        return sum;
    }
}
