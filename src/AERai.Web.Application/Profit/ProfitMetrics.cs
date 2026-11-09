namespace AERai.Web.Application.Profit;

/// <summary>
/// The figures of one P&amp;L row (a SKU, a family, a period, or a total), so every subtotal is the sum of its rows.
/// Money is signed as it reaches the seller: sales positive, every cost negative.
/// </summary>
/// <param name="Units">Units sold.</param>
/// <param name="RefundedUnits">Units refunded (their cost is given back).</param>
/// <param name="Sales">Product charges and taxes, and reimbursements.</param>
/// <param name="Refunds">What was given back to customers, net of the fees Amazon returned.</param>
/// <param name="AmazonFees">Referral fees, FBA fees, and shipping.</param>
/// <param name="Other">Promotions, closing fees, subscriptions, and the rest.</param>
/// <param name="Advertising">Ads API spend (negative), or billed ad charges for days before spend was synced.</param>
/// <param name="Cogs">Cost of the units sold less the cost of the units refunded (negative).</param>
/// <param name="CostReturned">The refunded units' cost (positive; already inside <paramref name="Cogs"/>).</param>
/// <param name="CostMissingUnits">Units sold with no cost set on their order date (counted at 0).</param>
/// <param name="Held">Of the amounts, what Amazon still holds.</param>
public sealed record ProfitMetrics(
    int Units,
    int RefundedUnits,
    decimal Sales,
    decimal Refunds,
    decimal AmazonFees,
    decimal Other,
    decimal Advertising,
    decimal Cogs,
    decimal CostReturned,
    int CostMissingUnits,
    decimal Held)
{
    /// <summary>No figures.</summary>
    public static ProfitMetrics Zero { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>What is left: every amount added up.</summary>
    public decimal Profit => Sales + Refunds + AmazonFees + Other + Advertising + Cogs;

    /// <summary>Profit as a share of sales, or <see langword="null"/> without sales.</summary>
    public decimal? Margin => Sales > 0 ? Profit / Sales : null;

    /// <summary>Whether nothing at all was recorded.</summary>
    public bool IsEmpty => this == Zero;

    /// <summary>Two rows added up.</summary>
    /// <param name="a">First.</param>
    /// <param name="b">Second.</param>
    /// <returns>The sum.</returns>
    public static ProfitMetrics operator +(ProfitMetrics a, ProfitMetrics b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return new(
            a.Units + b.Units, a.RefundedUnits + b.RefundedUnits, a.Sales + b.Sales, a.Refunds + b.Refunds, a.AmazonFees + b.AmazonFees,
            a.Other + b.Other, a.Advertising + b.Advertising, a.Cogs + b.Cogs, a.CostReturned + b.CostReturned,
            a.CostMissingUnits + b.CostMissingUnits, a.Held + b.Held);
    }

    /// <summary>Rows added up.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The sum (<see cref="Zero"/> for none).</returns>
    public static ProfitMetrics Sum(IEnumerable<ProfitMetrics> rows) => rows.Aggregate(Zero, (total, row) => total + row);
}
