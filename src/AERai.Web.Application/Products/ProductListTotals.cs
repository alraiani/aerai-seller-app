namespace AERai.Web.Application.Products;

/// <summary>
/// Counts for the Products page's tiles. They follow the search and family but not the cost
/// filter, so the tiles stay put when one is clicked.
/// </summary>
/// <param name="SkuCount">Products matching the search and family.</param>
/// <param name="WithCost">Of those, products with a cost in the marketplace.</param>
public sealed record ProductListTotals(int SkuCount, int WithCost)
{
    /// <summary>Products without a cost.</summary>
    public int MissingCost => SkuCount - WithCost;
}
