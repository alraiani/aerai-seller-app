using AERai.Seller.Domain.Staging;

namespace AERai.Seller.Application.Reports;

/// <summary>
/// Maps parsed GET_FBA_INVENTORY_PLANNING_DATA rows into per-state InventorySnapshot entities.
/// Column names below were confirmed against a real report pull (not the older "afn-*"-prefixed
/// names used by other/deprecated FBA inventory reports, which this report does not use at all).
/// The report has no "researching" quantity column, so InventoryState.Researching is never
/// populated from this parser — that state has no source in this particular report.
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

            AddIfPresent(snapshots, row, sku, "available", InventoryState.Available, snapshotDate, syncedAt);
            AddIfPresent(snapshots, row, sku, "Total Reserved Quantity", InventoryState.Reserved, snapshotDate, syncedAt);
            AddIfPresent(snapshots, row, sku, "unfulfillable-quantity", InventoryState.Unfulfillable, snapshotDate, syncedAt);
            AddIfPresent(snapshots, row, sku, "fc-transfer", InventoryState.FcTransfer, snapshotDate, syncedAt);
            AddIfPresent(snapshots, row, sku, "Reserved FC Processing", InventoryState.FcProcessing, snapshotDate, syncedAt);

            var inbound = SumIfPresent(row, "inbound-working", "inbound-shipped", "inbound-received");
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
