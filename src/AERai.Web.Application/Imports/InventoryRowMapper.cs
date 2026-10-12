using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Maps per-state inventory records to <see cref="StgInventoryRow"/>.
/// </summary>
public sealed class InventoryRowMapper : IStagingRowMapper
{
    /// <inheritdoc/>
    public ImportSource Source => ImportSource.Inventory;

    /// <inheritdoc/>
    public IReadOnlyList<string> RequiredColumns { get; } = ["snapshot-date", "sku", "state", "quantity"];

    /// <inheritdoc/>
    public void Append(ImportBatch batch, ParsedRecord record)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(record);

        batch.InventoryRows.Add(new StgInventoryRow
        {
            RowNumber = record.RowNumber,
            RawLine = record.RawLine,
            SnapshotDate = record.Get("snapshot-date"),
            Sku = record.Get("sku"),
            Asin = record.Get("asin"),
            ProductName = record.Get("product-name"),
            State = record.Get("state"),
            Quantity = record.Get("quantity"),
        });
    }
}
