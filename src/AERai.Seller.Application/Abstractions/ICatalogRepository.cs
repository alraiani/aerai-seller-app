using AERai.Seller.Domain.Staging;

namespace AERai.Seller.Application.Abstractions;

public interface ICatalogRepository
{
    /// <summary>Idempotent upsert keyed by Asin (items) and ParentAsin (parents) — safe to re-run.</summary>
    Task UpsertAsync(
        IEnumerable<CatalogItem> items, IEnumerable<CatalogParent> parents, CancellationToken cancellationToken = default);

    /// <summary>All catalog items, each with its ParentAsin set when it belongs to a variation family.</summary>
    Task<IReadOnlyList<CatalogItem>> GetAllItemsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogParent>> GetAllParentsAsync(CancellationToken cancellationToken = default);
}
