namespace AERai.Web.Application.Advertising;

/// <summary>
/// The natural direction of each advertising sort, shared by the service (which applies it) and the page
/// (which shows the arrow), so both agree on what "reversed" means.
/// </summary>
public static class AdSorts
{
    /// <summary>Whether the sort runs from high to low unless reversed.</summary>
    /// <param name="sort">Sort.</param>
    /// <returns>
    /// <see langword="true"/> for every advertising sort: the largest spend, sales, and clicks, and the most
    /// expensive ratios, come first, because those are the SKUs to look at.
    /// </returns>
    public static bool IsDescendingByDefault(this AdSort sort) =>
        sort is AdSort.Spend or AdSort.AdSales or AdSort.Acos or AdSort.Tacos or AdSort.Clicks or AdSort.TotalSales;
}
