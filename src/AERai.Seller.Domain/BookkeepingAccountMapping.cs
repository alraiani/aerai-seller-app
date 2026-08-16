namespace AERai.Seller.Domain;

/// <summary>
/// User-curated mapping from a raw Amazon settlement line-item type (AmountType/AmountDescription,
/// matching SettlementLineItem's columns) to the QuickBooks Desktop account name it should post to.
/// QuickBooksAccountName is null until the user maps it; multiple Amazon types can share the same
/// account name (e.g. several fee sub-types all mapped to one "Amazon Fees" account) — grouping for
/// the IIF export emerges from these user choices rather than any hardcoded bucket scheme.
/// </summary>
public class BookkeepingAccountMapping
{
    public int Id { get; set; }
    public required string AmountType { get; set; }
    public required string AmountDescription { get; set; }
    public string? QuickBooksAccountName { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
