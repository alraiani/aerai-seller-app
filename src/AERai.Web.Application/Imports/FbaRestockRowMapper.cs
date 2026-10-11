using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Maps Amazon's restock inventory report (<c>GET_RESTOCK_INVENTORY_RECOMMENDATIONS_REPORT</c>) to
/// <see cref="StgFbaRestockRow"/>. Amazon's headers are title case with spaces
/// (<c>Recommended replenishment qty</c>), which header normalization turns into lower-case hyphenated names.
/// </summary>
public sealed class FbaRestockRowMapper : IStagingRowMapper
{
    /// <inheritdoc/>
    public ImportSource Source => ImportSource.FbaRestockRecommendations;

    /// <inheritdoc/>
    public IReadOnlyList<string> RequiredColumns { get; } =
        ["merchant-sku", "recommended-replenishment-qty", "recommended-ship-date"];

    /// <inheritdoc/>
    public void Append(ImportBatch batch, ParsedRecord record)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(record);

        batch.FbaRestockRows.Add(new StgFbaRestockRow
        {
            RowNumber = record.RowNumber,
            RawLine = record.RawLine,
            Sku = record.Get("merchant-sku"),
            Asin = record.Get("asin"),
            ProductName = record.Get("product-name"),
            RecommendedQuantity = record.Get("recommended-replenishment-qty"),
            RecommendedShipDate = record.Get("recommended-ship-date"),
            RecommendedAction = record.Get("recommended-action"),
        });
    }
}
