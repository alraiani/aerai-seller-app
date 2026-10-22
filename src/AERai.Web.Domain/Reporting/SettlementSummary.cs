namespace AERai.Web.Domain.Reporting;

/// <summary>
/// Read model for <c>rpt.vw_SettlementSummary</c>: one row per settlement with amounts bucketed
/// into sales, refunds, expenses, and reserve (the columns of Seller Central's statement).
/// </summary>
public sealed class SettlementSummary
{
    /// <summary>Amazon settlement identifier.</summary>
    public required string SettlementId { get; set; }

    /// <summary>Marketplace the payout settles.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Start of the settlement period.</summary>
    public DateTimeOffset? PeriodStart { get; set; }

    /// <summary>End of the settlement period.</summary>
    public DateTimeOffset? PeriodEnd { get; set; }

    /// <summary>When Amazon paid the settlement out.</summary>
    public DateTimeOffset? DepositDate { get; set; }

    /// <summary>ISO 4217 currency the settlement was paid in.</summary>
    public string? Currency { get; set; }

    /// <summary>Currency the amounts convert into: the marketplace's currency.</summary>
    public required string HomeCurrency { get; set; }

    /// <summary>
    /// One unit of <see cref="Currency"/> in <see cref="HomeCurrency"/>: 1 when they are the same, the
    /// Operator's or the feed's rate otherwise, or <see langword="null"/> while no rate is known yet.
    /// </summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>Where <see cref="ExchangeRate"/> came from: <c>Same</c> (no conversion), <c>Market</c> (the daily feed), <c>Manual</c> (an Operator), or <see langword="null"/>.</summary>
    public string? RateSource { get; set; }

    /// <summary>Units sold in the settlement (refunds do not reduce it).</summary>
    public int Units { get; set; }

    /// <summary>Item price, shipping, and tax on orders (less the tax Amazon withholds), and FBA inventory reimbursements.</summary>
    public decimal Sales { get; set; }

    /// <summary>Everything Amazon charged (negative): fees, promotions, advertising, subscriptions, clawbacks.</summary>
    public decimal Expenses { get; set; }

    /// <summary>Net of refund transactions (usually negative).</summary>
    public decimal Refunds { get; set; }

    /// <summary>Account level reserve: held back from the payout (negative) or released from an earlier one (positive).</summary>
    public decimal Reserve { get; set; }

    /// <summary>Sum of all amounts — the payout.</summary>
    public decimal Net { get; set; }

    /// <summary><see cref="Sales"/> in <see cref="HomeCurrency"/>; <see langword="null"/> without a rate.</summary>
    public decimal? HomeSales { get; set; }

    /// <summary>The rest of <see cref="HomeNet"/> after converted sales, refunds, and reserve, so the four always add up to it; <see langword="null"/> without a rate.</summary>
    public decimal? HomeExpenses { get; set; }

    /// <summary><see cref="Refunds"/> in <see cref="HomeCurrency"/>; <see langword="null"/> without a rate.</summary>
    public decimal? HomeRefunds { get; set; }

    /// <summary><see cref="Reserve"/> in <see cref="HomeCurrency"/>; <see langword="null"/> without a rate.</summary>
    public decimal? HomeReserve { get; set; }

    /// <summary><see cref="Net"/> in <see cref="HomeCurrency"/> (the amount entered by an Operator when they gave one); <see langword="null"/> without a rate.</summary>
    public decimal? HomeNet { get; set; }
}
