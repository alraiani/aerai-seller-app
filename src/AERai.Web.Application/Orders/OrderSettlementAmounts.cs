namespace AERai.Web.Application.Orders;

/// <summary>What one settlement holds for one order, as paid (in the settlement's own currency).</summary>
/// <param name="AmazonOrderId">The order.</param>
/// <param name="SettlementId">The settlement the amounts were paid in.</param>
/// <param name="Currency">ISO 4217 currency the settlement was paid in.</param>
/// <param name="Sales">Item price amounts.</param>
/// <param name="Expenses">Fees and other charges on the order (negative).</param>
/// <param name="Refunds">Money returned to the customer (usually negative).</param>
/// <param name="Reserve">Account level reserve on the order's lines (normally none).</param>
public sealed record OrderSettlementAmounts(
    string AmazonOrderId,
    string SettlementId,
    string? Currency,
    decimal Sales,
    decimal Expenses,
    decimal Refunds,
    decimal Reserve)
{
    /// <summary>The order's net in this settlement.</summary>
    public decimal Net => Sales + Expenses + Refunds + Reserve;
}
