namespace AERai.Web.Domain.Core;

/// <summary>
/// One change of a SKU's unit cost in one marketplace: the cost that applies from <see cref="EffectiveFrom"/>
/// until the next change. <see cref="ProductCost"/> stays the current cost every page reads; this history
/// lets profit charge each sale the cost in effect when it was made, so a later cost change never
/// rewrites past profit.
/// </summary>
public sealed class ProductCostHistory
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>Marketplace the cost applies to.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Cost per unit in the marketplace's currency, or <see langword="null"/> when the cost was cleared.</summary>
    public decimal? CostOfGoods { get; set; }

    /// <summary>When this cost starts to apply (the time it was saved; seeded costs apply from the beginning).</summary>
    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>Who saved it (user email), or <c>system</c> for the costs seeded when history began.</summary>
    public required string ChangedBy { get; set; }
}
