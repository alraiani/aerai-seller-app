using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Profit;

/// <summary>Profit amounts of one local day, SKU, category, and held state, added up.</summary>
/// <param name="Day">The marketplace's local day it was posted (settlement line dates as they are).</param>
/// <param name="Sku">Seller SKU, or <see langword="null"/> for amounts not tied to a product.</param>
/// <param name="Category">What it counts toward.</param>
/// <param name="Held">Whether Amazon still holds the money.</param>
/// <param name="Amount">The amounts added up, signed.</param>
public sealed record ProfitAmountRow(DateOnly Day, string? Sku, ProfitCategory Category, bool Held, decimal Amount);

/// <summary>Units of a SKU sold on one local day at one unit cost (the cost in effect on each order's purchase date).</summary>
/// <param name="Day">The local day the sale was posted.</param>
/// <param name="Sku">Seller SKU.</param>
/// <param name="UnitCost">Cost per unit, or <see langword="null"/> when none was set on that date.</param>
/// <param name="Units">Units sold.</param>
public sealed record ProfitSoldUnitsRow(DateOnly Day, string Sku, decimal? UnitCost, int Units);

/// <summary>One refunded item's principal, with what is needed to tell how many units it gave back.</summary>
/// <param name="Day">The local day the refund was posted.</param>
/// <param name="Sku">Seller SKU.</param>
/// <param name="RefundedPrincipal">The principal given back (negative).</param>
/// <param name="ItemPrice">The order item's line total, when the order is known.</param>
/// <param name="ItemQuantity">The order item's units, when the order is known.</param>
/// <param name="UnitCost">Cost per unit on the order's purchase date, or <see langword="null"/> when none was set.</param>
public sealed record ProfitRefundRow(DateOnly Day, string Sku, decimal RefundedPrincipal, decimal? ItemPrice, int? ItemQuantity, decimal? UnitCost);

/// <summary>Ads API spend for a SKU on one local day.</summary>
/// <param name="Day">The day (the marketplace's local day, as Amazon Ads reports it).</param>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Spend">What the ads cost (positive).</param>
public sealed record ProfitAdSpendRow(DateOnly Day, string Sku, decimal Spend);

/// <summary>A SKU's catalog details, for the product cell.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Asin">ASIN, when known.</param>
/// <param name="Title">Title, when known.</param>
/// <param name="FamilyId">Family, when it has one.</param>
/// <param name="Family">Family name.</param>
/// <param name="ImagePath">Picture, when it has one.</param>
public sealed record ProfitProductRow(string Sku, string? Asin, string? Title, int? FamilyId, string? Family, string? ImagePath);

/// <summary>One settlement next to what the ledger says it holds.</summary>
/// <param name="SettlementId">Amazon's settlement id.</param>
/// <param name="PeriodStart">Period start.</param>
/// <param name="PeriodEnd">Period end.</param>
/// <param name="Net">What the settlement paid (its total, in its own currency).</param>
/// <param name="Reserve">Its reserve movements.</param>
/// <param name="LedgerTotal">The ledger transactions carrying its id added up, or <see langword="null"/> when there are none.</param>
/// <param name="LedgerTransactions">How many ledger transactions carry its id.</param>
public sealed record SettlementTieRow(string SettlementId, DateTimeOffset? PeriodStart, DateTimeOffset? PeriodEnd, decimal Net, decimal Reserve, decimal? LedgerTotal, int LedgerTransactions);

/// <summary>Ledger amounts the category rule counted as other expenses, by kind.</summary>
/// <param name="TransactionType">Amazon's transaction type.</param>
/// <param name="Detail">The breakdown path.</param>
/// <param name="Amount">Their amounts added up.</param>
/// <param name="Lines">How many lines.</param>
public sealed record ProfitUnmappedRow(string? TransactionType, string? Detail, decimal Amount, int Lines);
