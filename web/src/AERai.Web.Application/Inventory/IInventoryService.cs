using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>Builds each SKU's stock, sales velocity, and days of inventory for one marketplace.</summary>
public interface IInventoryService
{
    /// <summary>Every SKU with stock in a marketplace, most urgent first (see <see cref="InventorySort.Urgency"/>).</summary>
    /// <param name="marketplace">Marketplace to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>All items.</returns>
    Task<IReadOnlyList<InventoryItem>> GetItemsAsync(Marketplace marketplace, CancellationToken cancellationToken);

    /// <summary>Totals and one page of SKUs, filtered and sorted.</summary>
    /// <param name="marketplace">Marketplace to report on.</param>
    /// <param name="request">Paging; search matches SKU, ASIN, title, or family name.</param>
    /// <param name="filter">Family, status, and sort.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The overview.</returns>
    Task<InventoryOverview> GetOverviewAsync(Marketplace marketplace, PageRequest request, InventoryFilter filter, CancellationToken cancellationToken);
}
