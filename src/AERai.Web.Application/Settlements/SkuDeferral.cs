namespace AERai.Web.Application.Settlements;

/// <summary>What Amazon deferred out of a settlement for one SKU at one price each.</summary>
/// <param name="Sku">Seller SKU, or <see langword="null"/> when Amazon gave none.</param>
/// <param name="PriceEach">Product charges ÷ quantity, rounded to the cent like the settlement's own price each; <see langword="null"/> without a quantity.</param>
/// <param name="Quantity">Units.</param>
/// <param name="Amount">What will be (or was) paid out for them, after Amazon's fees.</param>
/// <param name="Orders">Distinct orders.</param>
/// <param name="Currency">ISO 4217 currency of <paramref name="Amount"/>.</param>
public sealed record SkuDeferral(string? Sku, decimal? PriceEach, int Quantity, decimal Amount, int Orders, string Currency);
