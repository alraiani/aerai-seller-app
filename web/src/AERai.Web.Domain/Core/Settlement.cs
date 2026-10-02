namespace AERai.Web.Domain.Core;

/// <summary>
/// A marketplace settlement (payout) period.
/// </summary>
public sealed class Settlement
{
    /// <summary>Amazon settlement identifier (natural key).</summary>
    public required string SettlementId { get; set; }

    /// <summary>Start of the settlement period.</summary>
    public DateTimeOffset? PeriodStart { get; set; }

    /// <summary>End of the settlement period.</summary>
    public DateTimeOffset? PeriodEnd { get; set; }

    /// <summary>ISO 4217 currency code.</summary>
    public string? Currency { get; set; }

    /// <summary>The staging batch that last replaced this settlement's lines (lineage).</summary>
    public long LastImportBatchId { get; set; }

    /// <summary>Individual amounts in the settlement.</summary>
    public List<SettlementLine> Lines { get; set; } = [];
}
