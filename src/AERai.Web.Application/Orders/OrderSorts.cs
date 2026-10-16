namespace AERai.Web.Application.Orders;

/// <summary>
/// The natural direction of each order sort, shared by the queries (which apply it) and the page
/// (which shows the arrow), so both agree on what "reversed" means.
/// </summary>
public static class OrderSorts
{
    /// <summary>Whether the sort runs from high to low unless reversed.</summary>
    /// <param name="sort">Sort.</param>
    /// <returns><see langword="true"/> for every order sort: newest, most units, and largest sales first.</returns>
    public static bool IsDescendingByDefault(this OrderSort sort) =>
        sort is OrderSort.Purchased or OrderSort.Units or OrderSort.Sales;
}
