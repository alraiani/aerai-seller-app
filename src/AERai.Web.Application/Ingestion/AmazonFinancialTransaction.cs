namespace AERai.Web.Application.Ingestion;

/// <summary>One transaction from Amazon's ledger (Finances API <c>listTransactions</c>), flattened to its amounts.</summary>
/// <param name="TransactionId">Amazon's transaction identifier.</param>
/// <param name="TransactionType">Amazon's transaction type, e.g. <c>Shipment</c>.</param>
/// <param name="Status">Amazon's status, e.g. <c>RELEASED</c> or <c>DEFERRED</c>.</param>
/// <param name="Description">Amazon's description, e.g. <c>Order Payment</c>.</param>
/// <param name="PostedDate">When Amazon posted it.</param>
/// <param name="AmazonOrderId">The order it belongs to (related identifier <c>ORDER_ID</c>), when any.</param>
/// <param name="SettlementId">The settlement it belongs to (related identifier <c>SETTLEMENT_ID</c>), when any.</param>
/// <param name="Amount">Its total, signed.</param>
/// <param name="Currency">ISO 4217 currency.</param>
/// <param name="Lines">Its amounts, adding up to <paramref name="Amount"/>.</param>
public sealed record AmazonFinancialTransaction(
    string TransactionId,
    string? TransactionType,
    string? Status,
    string? Description,
    DateTimeOffset PostedDate,
    string? AmazonOrderId,
    string? SettlementId,
    decimal Amount,
    string Currency,
    IReadOnlyList<AmazonFinancialLine> Lines);
