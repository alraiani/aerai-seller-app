namespace AERai.Seller.Domain;

/// <summary>
/// Single-row operational config for the bookkeeping export feature. Kept separate from the DPAPI-
/// encrypted AppSettings blob because it isn't a secret and belongs alongside the other EF-backed
/// bookkeeping tables (BookkeepingAccountMapping, BookkeepingExportRecord).
/// </summary>
public class BookkeepingSettings
{
    public int Id { get; set; }
    public string? DepositAccountName { get; set; }
    public string? ExportFolderPath { get; set; }
}
