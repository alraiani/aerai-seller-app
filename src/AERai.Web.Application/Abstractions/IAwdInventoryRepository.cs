using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Abstractions;

/// <summary>Writes AWD inventory snapshots to <c>core.AwdInventorySnapshot</c>.</summary>
public interface IAwdInventoryRepository
{
    /// <summary>
    /// Replaces a marketplace's AWD snapshot for one date with the given items, in one transaction.
    /// SKUs not yet in the product catalog are added to it.
    /// </summary>
    /// <param name="marketplaceId">Marketplace whose schedule pulled the stock.</param>
    /// <param name="snapshotDate">The date the snapshot is for.</param>
    /// <param name="items">Every SKU with AWD stock; SKUs left out have none on that date.</param>
    /// <param name="syncedAt">When the stock was pulled.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of rows written.</returns>
    Task<int> ReplaceSnapshotAsync(string marketplaceId, DateOnly snapshotDate, IReadOnlyCollection<AwdInventoryItem> items, DateTimeOffset syncedAt, CancellationToken cancellationToken);
}
