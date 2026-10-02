using AERai.Web.Application.Common;

namespace AERai.Web.Application.Products;

/// <summary>
/// Product master-data use cases.
/// </summary>
public interface IProductService
{
    /// <summary>Sets or clears a product's unit cost of goods.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="costOfGoods">Unit cost (0 – <see cref="ProductService.MaxCostOfGoods"/>), or <see langword="null"/> to clear.</param>
    /// <returns>Success, or a failure when the value is out of range or the SKU is unknown.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result> UpdateCostAsync(string sku, decimal? costOfGoods, CancellationToken cancellationToken);
}
