namespace AERai.Web.Application.Products;

/// <summary>One change of a SKU's unit cost, as listed on the product's edit page.</summary>
/// <param name="CostOfGoods">The cost from then on, or <see langword="null"/> when it was cleared.</param>
/// <param name="EffectiveFrom">When it started to apply.</param>
/// <param name="ChangedBy">Who saved it (<c>system</c> for the costs history began with).</param>
public sealed record ProductCostHistoryEntry(decimal? CostOfGoods, DateTimeOffset EffectiveFrom, string ChangedBy);
