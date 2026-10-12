namespace AERai.Web.Domain.Core;

/// <summary>
/// One generated QuickBooks IIF file for a settlement, kept so it can be downloaded again.
/// </summary>
/// <remarks>
/// The file text is stored with the row. A settlement can be re-promoted and its lines replaced, but an
/// export is a record of what was handed to the bookkeeper, so it never changes after it is written.
/// Exporting a settlement twice creates two rows.
/// </remarks>
public sealed class BookkeepingExport
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>Marketplace of the settlement.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Settlement the file was generated for.</summary>
    public required string SettlementId { get; set; }

    /// <summary>Download name, e.g. <c>Amazon-Settlement-123-20261011120000.iif</c>.</summary>
    public required string FileName { get; set; }

    /// <summary>The IIF file text.</summary>
    public required string Content { get; set; }

    /// <summary>Deposit account the file posted the payout to.</summary>
    public required string DepositAccountName { get; set; }

    /// <summary>Net payout in the file (the sum of every settlement line).</summary>
    public decimal NetTotal { get; set; }

    /// <summary>ISO 4217 currency code of the amounts.</summary>
    public string? Currency { get; set; }

    /// <summary>Start of the settlement period.</summary>
    public DateTimeOffset? PeriodStart { get; set; }

    /// <summary>End of the settlement period.</summary>
    public DateTimeOffset? PeriodEnd { get; set; }

    /// <summary>When the file was generated.</summary>
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>Email of the user who generated it.</summary>
    public required string GeneratedBy { get; set; }
}
