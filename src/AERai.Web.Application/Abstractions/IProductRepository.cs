using AERai.Web.Application.Common;
using AERai.Web.Application.Products;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Access to curated product master data.
/// </summary>
public interface IProductRepository
{
    /// <summary>Lists one page of products with their cost in one marketplace, filtered and sorted.</summary>
    /// <param name="marketplaceId">Marketplace whose costs to show and filter by.</param>
    /// <param name="filter">Cost filter, family, and sort.</param>
    /// <param name="request">Paging; every word of the search (<see cref="ProductSearch.Terms"/>) must match the SKU, ASIN, title, or family name.</param>
    /// <returns>One page of products.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<PagedResult<ProductSummary>> ListAsync(string marketplaceId, ProductListFilter filter, PageRequest request, CancellationToken cancellationToken);

    /// <summary>Counts products for the tiles, following the search and family but not the cost filter.</summary>
    /// <param name="marketplaceId">Marketplace whose costs to count.</param>
    /// <param name="familyId">Only this family, or <see langword="null"/> for all.</param>
    /// <param name="search">Search text, matched as in <see cref="ListAsync"/>.</param>
    /// <returns>The counts.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<ProductListTotals> GetTotalsAsync(string marketplaceId, int? familyId, string? search, CancellationToken cancellationToken);

    /// <summary>Gets the title and current cost of the given SKUs; SKUs not in the catalog are left out.</summary>
    /// <param name="marketplaceId">Marketplace whose costs to read.</param>
    /// <param name="skus">SKUs to look up.</param>
    /// <returns>A map from SKU (ordinal) to its title and cost.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<IReadOnlyDictionary<string, ProductCostSnapshot>> GetCostsAsync(string marketplaceId, IReadOnlyCollection<string> skus, CancellationToken cancellationToken);

    /// <summary>Lists every product with its cost for the downloadable cost sheet, ordered by family then SKU.</summary>
    /// <param name="marketplaceId">Marketplace whose costs to write.</param>
    /// <param name="familyId">Only this family, or <see langword="null"/> for all.</param>
    /// <returns>The sheet's rows.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<IReadOnlyList<ProductCostTemplateRow>> ListForCostSheetAsync(string marketplaceId, int? familyId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves several cost changes in one transaction. A change whose <see cref="ProductCostChange.Current"/>
    /// no longer equals the stored cost (or whose SKU is gone) is skipped and returned.
    /// </summary>
    /// <param name="marketplaceId">Marketplace the costs apply to.</param>
    /// <param name="changes">Validated changes, one per SKU, already rounded to cents.</param>
    /// <param name="updatedAt">Timestamp to record.</param>
    /// <returns>The SKUs skipped as stale.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<IReadOnlyList<string>> ApplyCostsAsync(string marketplaceId, IReadOnlyList<ProductCostChange> changes, DateTimeOffset updatedAt, CancellationToken cancellationToken);

    /// <summary>Gets one product with its cost in one marketplace.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="marketplaceId">Marketplace whose cost to show.</param>
    /// <returns>The product, or <see langword="null"/> when the SKU is unknown.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<ProductSummary?> GetAsync(string sku, string marketplaceId, CancellationToken cancellationToken);

    /// <summary>Sets or clears a product's cost of goods in one marketplace.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="marketplaceId">Marketplace the cost applies to.</param>
    /// <param name="costOfGoods">New unit cost, or <see langword="null"/> to clear it.</param>
    /// <param name="updatedAt">Timestamp to record.</param>
    /// <returns><see langword="true"/> when the product existed and was updated.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<bool> UpdateCostAsync(string sku, string marketplaceId, decimal? costOfGoods, DateTimeOffset updatedAt, CancellationToken cancellationToken);
}
