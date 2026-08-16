namespace AERai.Seller.Domain;

/// <summary>
/// Metadata for one generated IIF export file, one row per SettlementReport exported. The
/// generated-files list in the UI queries this table rather than scanning the export folder.
/// </summary>
public class BookkeepingExportRecord
{
    public int Id { get; set; }
    public required string SettlementId { get; set; }
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public decimal NetTotal { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public DateTimeOffset? DepositDate { get; set; }
}
