using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Settlements;

/// <summary>One deferred product of one order, for a settlement's Lines page.</summary>
/// <param name="TransactionId">Amazon's transaction identifier.</param>
/// <param name="AmazonOrderId">The order, when any.</param>
/// <param name="Quantity">Units.</param>
/// <param name="Amount">What will be (or was) paid out for it, after Amazon's fees.</param>
/// <param name="Currency">ISO 4217 currency of <paramref name="Amount"/>.</param>
/// <param name="DeferralReason">The deferral policy, e.g. <c>DD7</c>.</param>
/// <param name="MaturityDate">When Amazon releases (or released) it.</param>
/// <param name="Status">Still held, or released.</param>
/// <param name="ReleasedAt">When it was released, once it was.</param>
public sealed record DeferredLineItem(
    string TransactionId,
    string? AmazonOrderId,
    int Quantity,
    decimal Amount,
    string Currency,
    string? DeferralReason,
    DateTimeOffset? MaturityDate,
    DeferredTransactionStatus Status,
    DateTimeOffset? ReleasedAt);
