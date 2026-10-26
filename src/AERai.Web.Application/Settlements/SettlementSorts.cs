namespace AERai.Web.Application.Settlements;

/// <summary>
/// The natural direction of each settlement sort, shared by the queries (which apply it) and the
/// pages (which show the arrow), so both agree on what "reversed" means.
/// </summary>
public static class SettlementSorts
{
    /// <summary>Whether the settlement list sort runs from high to low unless reversed.</summary>
    /// <param name="sort">Sort.</param>
    /// <returns><see langword="true"/> for newest, largest sales, and largest net first.</returns>
    /// <remarks>Expenses, refunds, and ad costs are negative, so "largest first" is ascending.</remarks>
    public static bool IsDescendingByDefault(this SettlementSort sort) =>
        sort is SettlementSort.PeriodEnd or SettlementSort.Sales or SettlementSort.Net;

    /// <summary>Whether the per-SKU summary sort runs from high to low unless reversed.</summary>
    /// <param name="sort">Sort.</param>
    /// <returns><see langword="true"/> for sales, units, orders, net, and refunded expenses.</returns>
    /// <remarks>
    /// Expenses, refunds, and their parts are negative, so "largest first" is ascending; refunded
    /// expenses are mostly fees Amazon gives back (positive), so they run high to low. SKUs are A to Z.
    /// </remarks>
    public static bool IsDescendingByDefault(this SettlementSkuSort sort) =>
        sort is SettlementSkuSort.Sales or SettlementSkuSort.Units or SettlementSkuSort.Orders or SettlementSkuSort.Net
            or SettlementSkuSort.RefundedExpenses;

    /// <summary>Whether the order-group sort runs from high to low unless reversed.</summary>
    /// <param name="sort">Sort.</param>
    /// <returns><see langword="true"/> for amount only.</returns>
    public static bool IsDescendingByDefault(this SettlementGroupSort sort) => sort is SettlementGroupSort.Amount;
}
