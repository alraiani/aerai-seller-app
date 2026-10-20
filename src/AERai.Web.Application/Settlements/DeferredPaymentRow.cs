using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Settlements;

/// <summary>One deferred transaction, as the Deferred page and the settlement and order markers show it.</summary>
/// <param name="TransactionId">Amazon's transaction identifier.</param>
/// <param name="AmazonOrderId">The order it belongs to, when any.</param>
/// <param name="Description">Amazon's description, e.g. <c>Order Payment</c>.</param>
/// <param name="DeferralReason">The deferral policy, e.g. <c>DD7</c>.</param>
/// <param name="PostedDate">When Amazon posted it.</param>
/// <param name="MaturityDate">When Amazon releases (or released) the money.</param>
/// <param name="Amount">Its total (signed).</param>
/// <param name="Currency">ISO 4217 currency.</param>
/// <param name="Status">Still held, or released.</param>
/// <param name="ReleasedAt">When it was released, once it was.</param>
public sealed record DeferredPaymentRow(
    string TransactionId,
    string? AmazonOrderId,
    string? Description,
    string? DeferralReason,
    DateTimeOffset? PostedDate,
    DateTimeOffset? MaturityDate,
    decimal Amount,
    string? Currency,
    DeferredTransactionStatus Status,
    DateTimeOffset? ReleasedAt);
