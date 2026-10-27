using System.Globalization;
using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Maps Amazon's FBA inventory planning report (<c>GET_FBA_INVENTORY_PLANNING_DATA</c>) to
/// <see cref="StgFbaPlanningRow"/>. Amazon adds and renames columns in this report from time to time, so only the
/// SKU is required; any other column it no longer sends stages as blank.
/// </summary>
public sealed class FbaPlanningRowMapper : IStagingRowMapper
{
    /// <summary>Prefix of the aged-inventory surcharge columns, one per age band (e.g. <c>estimated-ais-181-210-days</c>).</summary>
    private const string AgedSurchargePrefix = "estimated-ais-";

    /// <inheritdoc/>
    public ImportSource Source => ImportSource.FbaInventoryPlanning;

    /// <inheritdoc/>
    public IReadOnlyList<string> RequiredColumns { get; } = ["sku", "inv-age-0-to-90-days", "estimated-excess-quantity"];

    /// <inheritdoc/>
    public void Append(ImportBatch batch, ParsedRecord record)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(record);

        batch.FbaPlanningRows.Add(new StgFbaPlanningRow
        {
            RowNumber = record.RowNumber,
            RawLine = record.RawLine,
            Sku = record.Get("sku"),
            Asin = record.Get("asin"),
            ProductName = record.Get("product-name"),
            Marketplace = record.Get("marketplace"),
            Age0To90 = record.Get("inv-age-0-to-90-days"),
            Age91To180 = record.Get("inv-age-91-to-180-days"),
            Age181To270 = record.Get("inv-age-181-to-270-days"),
            Age271To365 = record.Get("inv-age-271-to-365-days"),
            Age365Plus = record.Get("inv-age-365-plus-days"),
            EstimatedExcess = record.Get("estimated-excess-quantity"),
            DaysOfSupply = record.Get("days-of-supply"),
            StorageCostNextMonth = record.Get("estimated-storage-cost-next-month"),
            AgedSurchargeNextMonth = AgedSurcharge(record),
            Currency = record.Get("currency"),
        });
    }

    /// <summary>
    /// Adds up the surcharge bands Amazon reports; blank when none has a number, so promotion stores "unknown"
    /// rather than zero. Unreadable bands are skipped (the raw line keeps them).
    /// </summary>
    private static string? AgedSurcharge(ParsedRecord record)
    {
        decimal? total = null;
        foreach (var (column, value) in record.Fields)
        {
            if (column.StartsWith(AgedSurchargePrefix, StringComparison.Ordinal)
                && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                total = (total ?? 0) + amount;
            }
        }

        return total?.ToString(CultureInfo.InvariantCulture);
    }
}
