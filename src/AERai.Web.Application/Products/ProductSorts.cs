namespace AERai.Web.Application.Products;

/// <summary>
/// The natural direction of each product sort, shared by the query (which applies it) and the page
/// (which shows the arrow), so both agree on what "reversed" means.
/// </summary>
public static class ProductSorts
{
    /// <summary>Whether the sort runs from high to low unless reversed.</summary>
    /// <param name="sort">Sort.</param>
    /// <returns><see langword="true"/> for cost (highest first) and updated (newest first).</returns>
    public static bool IsDescendingByDefault(this ProductSort sort) => sort is ProductSort.Cost or ProductSort.Updated;
}
