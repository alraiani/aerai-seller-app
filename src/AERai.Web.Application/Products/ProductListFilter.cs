namespace AERai.Web.Application.Products;

/// <summary>Which products the Products list shows, and in what order (search and paging are in the page request).</summary>
/// <param name="Cost">Only products with, or without, a cost in the marketplace.</param>
/// <param name="FamilyId">Only products in this family, or <see langword="null"/> for all.</param>
/// <param name="Sort">Order.</param>
/// <param name="Descending">Reverse the sort's natural direction (see <see cref="ProductSorts"/>).</param>
public sealed record ProductListFilter(
    ProductCostFilter Cost = ProductCostFilter.All,
    int? FamilyId = null,
    ProductSort Sort = ProductSort.Sku,
    bool Descending = false)
{
    /// <summary>Every product, by SKU.</summary>
    public static ProductListFilter Default { get; } = new();
}
