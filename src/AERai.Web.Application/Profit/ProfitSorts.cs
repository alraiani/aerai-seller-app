namespace AERai.Web.Application.Profit;

/// <summary>
/// The natural direction of each profit sort, shared by the service (which applies it) and the page (which shows
/// the arrow), so both agree on what "reversed" means.
/// </summary>
public static class ProfitSorts
{
    /// <summary>Whether the sort runs from high to low unless reversed.</summary>
    /// <param name="sort">Sort.</param>
    /// <returns>
    /// <see langword="true"/> for profit, margin, units, and sales (the best first); <see langword="false"/> for the
    /// SKU (A–Z) and for the costs, which are negative, so the biggest cost comes first.
    /// </returns>
    public static bool IsDescendingByDefault(this ProfitSort sort) =>
        sort is ProfitSort.Profit or ProfitSort.Margin or ProfitSort.Units or ProfitSort.Sales;
}
