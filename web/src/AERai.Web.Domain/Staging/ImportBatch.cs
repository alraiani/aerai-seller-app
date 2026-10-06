namespace AERai.Web.Domain.Staging;

/// <summary>
/// One uploaded file's worth of raw rows in the staging schema, plus the audit trail of its promotion.
/// </summary>
public sealed class ImportBatch
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>The report type the file contains.</summary>
    public ImportSource Source { get; set; }

    /// <summary>Marketplace the file's data belongs to; promotion stamps it on every curated row.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Original file name as supplied by the uploader (display only; never used as a path).</summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Path of the untouched original file in the raw blob container (the landing zone). Lets a
    /// batch be re-staged later without the file being uploaded again.
    /// </summary>
    public string? RawFilePath { get; set; }

    /// <summary>Lower-case hex SHA-256 of the raw file, for integrity checks and duplicate detection.</summary>
    public string? RawFileSha256 { get; set; }

    /// <summary>User name (email) of the person who uploaded the file.</summary>
    public required string UploadedBy { get; set; }

    /// <summary>When the file was received.</summary>
    public DateTimeOffset UploadedAt { get; set; }

    /// <summary>Current lifecycle state.</summary>
    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Received;

    /// <summary>Number of data rows parsed from the file.</summary>
    public int RowCount { get; set; }

    /// <summary>Rows promoted into <c>core</c> on the most recent promotion.</summary>
    public int PromotedRowCount { get; set; }

    /// <summary>Rows rejected on the most recent promotion.</summary>
    public int RejectedRowCount { get; set; }

    /// <summary>When the batch was last promoted; <see langword="null"/> if never.</summary>
    public DateTimeOffset? PromotedAt { get; set; }

    /// <summary>Batch-level failure reason when <see cref="Status"/> is <see cref="ImportBatchStatus.Failed"/>.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Raw order lines (only populated for <see cref="ImportSource.Orders"/> batches).</summary>
    public List<StgOrderLine> OrderLines { get; set; } = [];

    /// <summary>Raw inventory rows (only populated for <see cref="ImportSource.Inventory"/> batches).</summary>
    public List<StgInventoryRow> InventoryRows { get; set; } = [];

    /// <summary>Raw FBA inventory rows (only populated for <see cref="ImportSource.FbaInventory"/> batches).</summary>
    public List<StgFbaInventoryRow> FbaInventoryRows { get; set; } = [];

    /// <summary>Raw reserved-inventory rows (only populated for <see cref="ImportSource.FbaReservedInventory"/> batches).</summary>
    public List<StgFbaReservedRow> FbaReservedRows { get; set; } = [];

    /// <summary>Raw settlement lines (only populated for <see cref="ImportSource.Settlements"/> batches).</summary>
    public List<StgSettlementLine> SettlementLines { get; set; } = [];
}
