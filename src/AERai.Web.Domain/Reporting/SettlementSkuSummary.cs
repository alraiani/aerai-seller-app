namespace AERai.Web.Domain.Reporting;

/// <summary>
/// Read model for <c>rpt.vw_SettlementSkuSummary</c>: one row per settlement, SKU, sale price, and
/// currency, with the same Sales/Refunds/Expenses/Reserve columns as <see cref="SettlementSummary"/>.
/// </summary>
public sealed class SettlementSkuSummary
{
    /// <summary>Marketplace the payout settles.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Amazon settlement identifier.</summary>
    public required string SettlementId { get; set; }

    /// <summary>Start of the settlement period.</summary>
    public DateTimeOffset? PeriodStart { get; set; }

    /// <summary>End of the settlement period.</summary>
    public DateTimeOffset? PeriodEnd { get; set; }

    /// <summary>ISO 4217 currency the settlement was paid in.</summary>
    public string? Currency { get; set; }

    /// <summary>Currency the amounts convert into: the marketplace's currency.</summary>
    public required string HomeCurrency { get; set; }

    /// <summary>
    /// One unit of <see cref="Currency"/> in <see cref="HomeCurrency"/>: 1 when they are the same, the
    /// Operator's or the feed's rate otherwise, or <see langword="null"/> while no rate is known yet.
    /// </summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>Seller SKU; <see langword="null"/> for amounts that belong to no SKU (service fees).</summary>
    public string? Sku { get; set; }

    /// <summary>ASIN from the product catalog, when the SKU is known there.</summary>
    public string? Asin { get; set; }

    /// <summary>Product title from the catalog, when known.</summary>
    public string? Title { get; set; }

    /// <summary>Price per item: the Principal divided by the units on the order item (the whole Principal when the units are unknown); <see langword="null"/> when the row has none.</summary>
    public decimal? SalePrice { get; set; }

    /// <summary>Units sold at this price (quantity on the order price lines; refunds do not reduce it).</summary>
    public int Units { get; set; }

    /// <summary>Distinct orders in the group.</summary>
    public int OrderCount { get; set; }

    /// <summary>Settlement lines in the group.</summary>
    public int LineCount { get; set; }

    /// <summary>Item price, shipping, and tax on orders (less the tax Amazon withholds), and FBA inventory reimbursements.</summary>
    public decimal Sales { get; set; }

    /// <summary>Everything Amazon charged (negative): fees, promotions, advertising, subscriptions, clawbacks.</summary>
    public decimal Expenses { get; set; }

    /// <summary>Net of refund transactions (usually negative).</summary>
    public decimal Refunds { get; set; }

    /// <summary>Account level reserve: held back from the payout (negative) or released from an earlier one (positive).</summary>
    public decimal Reserve { get; set; }

    /// <summary>The part of <see cref="Refunds"/> that gave back price, shipping, and tax.</summary>
    public decimal RefundedSales { get; set; }

    /// <summary>The part of <see cref="Refunds"/> that gave back or charged fees.</summary>
    public decimal RefundedExpenses { get; set; }

    /// <summary>The part of <see cref="Expenses"/> spent on shipping to customers and into Amazon.</summary>
    public decimal Shipping { get; set; }

    /// <summary>The part of <see cref="Expenses"/> that is FBA fees (fulfillment, storage, placement, returns, removals, AWD).</summary>
    public decimal FbaFees { get; set; }

    /// <summary>The part of <see cref="Expenses"/> that is Amazon's referral fees.</summary>
    public decimal Commission { get; set; }

    /// <summary>The rest of <see cref="Expenses"/> (advertising, subscriptions, deals, promotions, clawbacks, deductions).</summary>
    public decimal OtherExpenses { get; set; }

    /// <summary>Sum of all amounts in the group.</summary>
    public decimal Net { get; set; }

    /// <summary><see cref="SalePrice"/> in <see cref="HomeCurrency"/>; <see langword="null"/> without a rate or a price.</summary>
    public decimal? HomeSalePrice { get; set; }

    /// <summary><see cref="Sales"/> in <see cref="HomeCurrency"/>; <see langword="null"/> without a rate.</summary>
    public decimal? HomeSales { get; set; }

    /// <summary><see cref="Expenses"/> in <see cref="HomeCurrency"/>; <see langword="null"/> without a rate.</summary>
    public decimal? HomeExpenses { get; set; }

    /// <summary><see cref="Refunds"/> in <see cref="HomeCurrency"/>; <see langword="null"/> without a rate.</summary>
    public decimal? HomeRefunds { get; set; }

    /// <summary><see cref="Reserve"/> in <see cref="HomeCurrency"/>; <see langword="null"/> without a rate.</summary>
    public decimal? HomeReserve { get; set; }

    /// <summary><see cref="RefundedSales"/> in <see cref="HomeCurrency"/>; <see langword="null"/> without a rate.</summary>
    public decimal? HomeRefundedSales { get; set; }

    /// <summary>The rest of <see cref="HomeRefunds"/> after converted refunded sales; <see langword="null"/> without a rate.</summary>
    public decimal? HomeRefundedExpenses { get; set; }

    /// <summary><see cref="Shipping"/> in <see cref="HomeCurrency"/>; <see langword="null"/> without a rate.</summary>
    public decimal? HomeShipping { get; set; }

    /// <summary><see cref="FbaFees"/> in <see cref="HomeCurrency"/>; <see langword="null"/> without a rate.</summary>
    public decimal? HomeFbaFees { get; set; }

    /// <summary><see cref="Commission"/> in <see cref="HomeCurrency"/>; <see langword="null"/> without a rate.</summary>
    public decimal? HomeCommission { get; set; }

    /// <summary>The rest of <see cref="HomeExpenses"/> after converted shipping, FBA fees, and commission; <see langword="null"/> without a rate.</summary>
    public decimal? HomeOtherExpenses { get; set; }

    /// <summary><see cref="Net"/> in <see cref="HomeCurrency"/>; <see langword="null"/> without a rate.</summary>
    public decimal? HomeNet { get; set; }
}
