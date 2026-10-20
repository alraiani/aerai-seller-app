namespace AERai.Web.Application.Orders;

/// <summary>
/// What Amazon has paid out for one order so far, added up over every settlement with lines for it (a
/// sale and a later refund are often in different settlements). Amounts are in the order's currency.
/// </summary>
/// <param name="SettlementIds">The settlements, in the order their lines were found.</param>
/// <param name="Sales">Item price amounts.</param>
/// <param name="Expenses">Fees and other charges on the order (negative).</param>
/// <param name="Refunds">Money returned to the customer (usually negative).</param>
/// <param name="Reserve">Account level reserve (never on an order's own lines; kept so the columns match the settlement pages).</param>
public sealed record OrderPayout(IReadOnlyList<string> SettlementIds, decimal Sales, decimal Expenses, decimal Refunds, decimal Reserve)
{
    /// <summary>What was received for the order.</summary>
    public decimal Net => Sales + Expenses + Refunds + Reserve;

    /// <summary>Adds up an order's settlement amounts.</summary>
    /// <param name="amounts">The order's amounts, one per settlement.</param>
    /// <returns>The payout, or <see langword="null"/> when no settlement has lines for the order yet.</returns>
    public static OrderPayout? From(IReadOnlyCollection<OrderSettlementAmounts> amounts)
    {
        ArgumentNullException.ThrowIfNull(amounts);
        if (amounts.Count == 0)
        {
            return null;
        }

        return new OrderPayout(
            amounts.Select(a => a.SettlementId).Distinct(StringComparer.Ordinal).ToList(),
            amounts.Sum(a => a.Sales),
            amounts.Sum(a => a.Expenses),
            amounts.Sum(a => a.Refunds),
            amounts.Sum(a => a.Reserve));
    }
}
