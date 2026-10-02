using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Maps Amazon's native FBA inventory report (<c>GET_FBA_MYI_ALL_INVENTORY_DATA</c>) to
/// <see cref="StgFbaInventoryRow"/>, keeping its wide layout; promotion unpivots it.
/// </summary>
public sealed class FbaInventoryRowMapper : IStagingRowMapper
{
    /// <inheritdoc/>
    public ImportSource Source => ImportSource.FbaInventory;

    /// <inheritdoc/>
    public IReadOnlyList<string> RequiredColumns { get; } = ["sku", "afn-fulfillable-quantity"];

    /// <inheritdoc/>
    public void Append(ImportBatch batch, ParsedRecord record)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(record);

        batch.FbaInventoryRows.Add(new StgFbaInventoryRow
        {
            RowNumber = record.RowNumber,
            RawLine = record.RawLine,
            Sku = record.Get("sku"),
            Asin = record.Get("asin"),
            ProductName = record.Get("product-name"),
            AfnFulfillableQuantity = record.Get("afn-fulfillable-quantity"),
            AfnUnsellableQuantity = record.Get("afn-unsellable-quantity"),
            AfnReservedQuantity = record.Get("afn-reserved-quantity"),
            AfnInboundWorkingQuantity = record.Get("afn-inbound-working-quantity"),
            AfnInboundShippedQuantity = record.Get("afn-inbound-shipped-quantity"),
            AfnInboundReceivingQuantity = record.Get("afn-inbound-receiving-quantity"),
        });
    }
}
