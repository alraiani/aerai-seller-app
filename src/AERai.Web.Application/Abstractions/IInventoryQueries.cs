using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Read-only queries behind the Inventory page. Returns raw rows; velocity, days of inventory,
/// filtering, and totals are computed by <see cref="InventoryService"/>.
/// </summary>
public interface IInventoryQueries
{
    /// <summary>Every SKU's current inventory position in a marketplace.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>All positions.</returns>
    Task<IReadOnlyList<InventoryPosition>> GetPositionsAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>Units sold per order line since a point in time.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="since">Lower bound on purchase time (inclusive).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Matching lines.</returns>
    Task<IReadOnlyList<UnitsSold>> GetUnitsSoldAsync(string marketplaceId, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Every SKU's own lead-time overrides in a marketplace (SKUs without any are absent).</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Overrides keyed by SKU.</returns>
    Task<IReadOnlyDictionary<string, LeadTimeSettings>> GetLeadTimesAsync(string marketplaceId, CancellationToken cancellationToken);
}
