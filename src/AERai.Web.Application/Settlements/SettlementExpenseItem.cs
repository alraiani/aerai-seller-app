namespace AERai.Web.Application.Settlements;

/// <summary>One named part of a settlement amount, e.g. "Advertising −12,400.00".</summary>
/// <param name="Label">What the money was for, in words.</param>
/// <param name="Amount">How much, in the currency the page shows.</param>
public sealed record SettlementExpenseItem(string Label, decimal Amount);
