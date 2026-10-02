using AERai.Web.Application.Common;
using AERai.Web.Application.Products;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Access to curated product master data.
/// </summary>
public interface IProductRepository
{
    /// <summary>Lists products ordered by SKU.</summary>
    /// <param name="request">Paging; search matches SKU, ASIN, or title.</param>
    /// <returns>One page of products.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<PagedResult<ProductSummary>> ListAsync(PageRequest request, CancellationToken cancellationToken);

    /// <summary>Gets one product.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <returns>The product, or <see langword="null"/> when the SKU is unknown.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<ProductSummary?> GetAsync(string sku, CancellationToken cancellationToken);

    /// <summary>Sets a product's cost of goods.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="costOfGoods">New unit cost, or <see langword="null"/> to clear it.</param>
    /// <param name="updatedAt">Timestamp to record.</param>
    /// <returns><see langword="true"/> when the product existed and was updated.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<bool> UpdateCostAsync(string sku, decimal? costOfGoods, DateTimeOffset updatedAt, CancellationToken cancellationToken);
}
