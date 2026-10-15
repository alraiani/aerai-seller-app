using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Maps Amazon's reserved inventory report (<c>GET_RESERVED_INVENTORY_DATA</c>) to
/// <see cref="StgFbaReservedRow"/>; promotion unpivots it into the reserved states. Amazon's
/// headers use underscores (<c>reserved_fc-processing</c>), which header normalization turns into hyphens.
/// </summary>
public sealed class FbaReservedInventoryRowMapper : IStagingRowMapper
{
    /// <inheritdoc/>
    public ImportSource Source => ImportSource.FbaReservedInventory;

    /// <inheritdoc/>
    public IReadOnlyList<string> RequiredColumns { get; } =
        ["sku", "reserved-customerorders", "reserved-fc-transfers", "reserved-fc-processing"];

    /// <inheritdoc/>
    /// <remarks>
    /// Amazon's live report (seen on US in October 2026) has no <c>reserved_fc-transfers</c>
    /// column: it reports <c>reserved_staging</c> in its place (with <c>reserved_qty</c> still the
    /// sum of the three reasons), so staging units are read into the transfers bucket. Like
    /// transfers, they are still in Amazon's network and not yet sold.
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ColumnAliases { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["reserved-fc-transfers"] = ["reserved-staging"],
        };

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
