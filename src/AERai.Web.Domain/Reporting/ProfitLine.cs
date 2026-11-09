namespace AERai.Web.Domain.Reporting;

/// <summary>
/// One amount of profit &amp; loss (<c>rpt.vw_ProfitLine</c>): a ledger line, or a settlement line from before the
/// marketplace's ledger begins, with its category. Keyless read model.
/// </summary>
public sealed class ProfitLine
{
    /// <summary>The marketplace whose profit it counts in.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary><c>Ledger</c> or <c>Settlement</c>.</summary>
    public required string Source { get; set; }

    /// <summary>
    /// When Amazon posted it. Ledger times are exact; settlement line dates are date-only, stored at midnight UTC,
    /// so their date is used as it is.
    /// </summary>
    public DateTimeOffset PostedDate { get; set; }

    /// <summary>The order it belongs to, when any.</summary>
    public string? AmazonOrderId { get; set; }

    /// <summary>The settlement it belongs to, when known.</summary>
    public string? SettlementId { get; set; }

    /// <summary>Amazon's transaction type.</summary>
    public string? TransactionType { get; set; }

    /// <summary>The breakdown path (ledger) or amount type/description (settlement).</summary>
    public string? Detail { get; set; }

    /// <summary>Seller SKU, or <see langword="null"/> for an amount not tied to a product.</summary>
    public string? Sku { get; set; }

    /// <summary>Units sold, on one line per item only.</summary>
    public int Units { get; set; }

    /// <summary>The amount, signed (money to the seller is positive).</summary>
    public decimal Amount { get; set; }

    /// <summary>Whether Amazon still holds the money.</summary>
    public bool Held { get; set; }

    /// <summary>Whether this is an item's principal (its price before tax).</summary>
    public bool IsPrincipal { get; set; }

    /// <summary>What it counts toward.</summary>
    public ProfitCategory Category { get; set; }
}
