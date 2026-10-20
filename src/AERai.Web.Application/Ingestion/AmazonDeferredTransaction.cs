namespace AERai.Web.Application.Ingestion;

/// <summary>One transaction from Amazon's deferred transactions listing (Finances API <c>listTransactions</c>).</summary>
/// <param name="TransactionId">Amazon's transaction identifier.</param>
/// <param name="AmazonOrderId">The order it belongs to (related identifier <c>ORDER_ID</c>), when any.</param>
/// <param name="SettlementId">The settlement the sale was made in (related identifier <c>SETTLEMENT_ID</c>), when any.</param>
/// <param name="TransactionType">Amazon's transaction type, e.g. <c>Shipment</c>.</param>
/// <param name="Description">Amazon's description, e.g. <c>Order Payment</c>.</param>
/// <param name="PostedDate">When Amazon posted it.</param>
/// <param name="Amount">Its total (signed).</param>
/// <param name="Currency">ISO 4217 currency of <paramref name="Amount"/>.</param>
/// <param name="DeferralReason">The deferral policy, e.g. <c>DD7</c> or <c>B2B</c>.</param>
/// <param name="MaturityDate">When the money is (or was) released.</param>
/// <param name="Products">The products it pays for.</param>
public sealed record AmazonDeferredTransaction(
    string TransactionId,
    string? AmazonOrderId,
    string? SettlementId,
    string? TransactionType,
    string? Description,
    DateTimeOffset? PostedDate,
    decimal Amount,
    string? Currency,
    string? DeferralReason,
    DateTimeOffset? MaturityDate,
    IReadOnlyList<AmazonDeferredProduct> Products);
