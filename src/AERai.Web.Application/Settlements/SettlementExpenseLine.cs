namespace AERai.Web.Application.Settlements;

/// <summary>
/// The settlement lines of one kind of charge behind a per-SKU summary row, added up: what a
/// settlement's Other expenses are made of.
/// </summary>
/// <param name="Sku">The row's SKU, or <see langword="null"/> for amounts that belong to no SKU.</param>
/// <param name="SalePrice">The row's price per item, as paid, or <see langword="null"/> when it has none.</param>
/// <param name="TransactionType">Amazon's transaction type, e.g. ServiceFee.</param>
/// <param name="AmountType">Amazon's amount type, e.g. Cost of Advertising.</param>
/// <param name="AmountDescription">Amazon's amount description, e.g. Subscription Fee.</param>
/// <param name="Amount">The lines' total, as paid.</param>
/// <param name="HomeAmount">The lines' total in the marketplace's currency, or <see langword="null"/> without a rate.</param>
public sealed record SettlementExpenseLine(
    string? Sku,
    decimal? SalePrice,
    string TransactionType,
    string? AmountType,
    string? AmountDescription,
    decimal Amount,
    decimal? HomeAmount);
