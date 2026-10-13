using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Settlements;

/// <summary>
/// The settlement lines of one order behind a per-SKU summary row (or, for lines without an order,
/// all lines of one transaction type), with their totals: the parent row of the Lines page.
/// </summary>
/// <param name="AmazonOrderId">The order, or <see langword="null"/> when the group holds lines without one.</param>
/// <param name="TransactionType">For a group without an order, the transaction type its lines share; otherwise <see langword="null"/>.</param>
/// <param name="FirstPosted">When the group's earliest line posted.</param>
/// <param name="Units">Units on the group's price lines.</param>
/// <param name="Amount">Sum of the group's amounts.</param>
/// <param name="Sales">The part of <paramref name="Amount"/> that counts as sales.</param>
/// <param name="Fees">The part that counts as fees.</param>
/// <param name="Refunds">The part that counts as refunds.</param>
/// <param name="Other">The part that counts as other.</param>
/// <param name="Lines">Every line in the group, earliest posted first.</param>
public sealed record SettlementOrderGroup(
    string? AmazonOrderId,
    string? TransactionType,
    DateTimeOffset? FirstPosted,
    int Units,
    decimal Amount,
    decimal Sales,
    decimal Fees,
    decimal Refunds,
    decimal Other,
    IReadOnlyList<SettlementLineDetail> Lines)
{
    /// <summary>Whether the group is one order (otherwise it is one transaction type without an order).</summary>
    public bool IsOrder => AmazonOrderId is not null;

    /// <summary>The distinct transaction types in the group, in the order they first appear (e.g. "Order, Refund").</summary>
    public IReadOnlyList<string> TransactionTypes => Lines.Select(l => l.TransactionType).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
