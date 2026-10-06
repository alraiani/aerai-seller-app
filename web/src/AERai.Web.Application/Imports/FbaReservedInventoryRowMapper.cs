using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Maps Amazon's reserved inventory report (<c>GET_RESERVED_INVENTORY_DATA</c>) to
/// <see cref="StgFbaReservedRow"/>; promotion unpivots it into the reserved states. Amazon's
/// headers use underscores (<c>reserved_fc-transfers</c>), which header normalization turns into hyphens.
/// </summary>
public sealed class FbaReservedInventoryRowMapper : IStagingRowMapper
{
    /// <inheritdoc/>
    public ImportSource Source => ImportSource.FbaReservedInventory;

    /// <inheritdoc/>
    public IReadOnlyList<string> RequiredColumns { get; } =
        ["sku", "reserved-customerorders", "reserved-fc-transfers", "reserved-fc-processing"];

    /// <inheritdoc/>
    public void Append(ImportBatch batch, ParsedRecord record)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(record);

        batch.FbaReservedRows.Add(new StgFbaReservedRow
        {
            RowNumber = record.RowNumber,
            RawLine = record.RawLine,
            Sku = record.Get("sku"),
            Asin = record.Get("asin"),
            ProductName = record.Get("product-name"),
            ReservedQuantity = record.Get("reserved-qty"),
            ReservedCustomerOrders = record.Get("reserved-customerorders"),
            ReservedFcTransfers = record.Get("reserved-fc-transfers"),
            ReservedFcProcessing = record.Get("reserved-fc-processing"),
        });
    }
}
