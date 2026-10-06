using AERai.Web.Application.Common;
using AERai.Web.Application.Products;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Access to curated product master data.
/// </summary>
public interface IProductRepository
{
    /// <summary>Lists products ordered by SKU, with their cost in one marketplace.</summary>
    /// <param name="marketplaceId">Marketplace whose costs to show.</param>
    /// <param name="request">Paging; search matches SKU, ASIN, or title.</param>
    /// <returns>One page of products.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<PagedResult<ProductSummary>> ListAsync(string marketplaceId, PageRequest request, CancellationToken cancellationToken);

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
