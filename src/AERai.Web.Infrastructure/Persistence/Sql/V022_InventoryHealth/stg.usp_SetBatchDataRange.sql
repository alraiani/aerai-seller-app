/*
  stg.usp_SetBatchDataRange  (V022: the FBA inventory planning report is a snapshot too)
  Purpose      : Records which dates a staged file's data covers, in stg.ImportBatch.DataStart/DataEnd,
                 so the batch list can show them without opening the file.
  Inputs       : @BatchId - the batch to set, or NULL to set every batch (used by migrations).
  Side effects : Updates stg.ImportBatch.DataStart/DataEnd only. Never touches rows, core, or status.
  Rules        : Orders      purchase-date of the rows (earliest to latest).
                 Settlements the settlement period from the header row (settlement-start-date to
                             settlement-end-date). Only a file with no header dates falls back to the posted
                             dates of its lines: a settlement can include lines posted before its period
                             opened, and those must not stretch the range.
                 Inventory   snapshot-date of the rows.
                 FBA inventory, reserved, restock, inventory planning snapshots: dated by the day the batch
                 was received (they are dated that way when promoted too).
                 Dates use stg.ufn_TryParseDateTimeOffset, the same parser promotion uses, so a date that
                 promotes also counts here. Unparseable dates are ignored; if none parse the range is NULL.
  Idempotency  : Safe to run repeatedly; it recomputes from the staged rows.
*/
CREATE OR ALTER PROCEDURE stg.usp_SetBatchDataRange
    @BatchId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE b
    SET DataStart = r.DataStart,
        DataEnd = r.DataEnd
    FROM stg.ImportBatch AS b
    LEFT JOIN (
        SELECT o.ImportBatchId AS BatchId,
               MIN(stg.ufn_TryParseDateTimeOffset(o.PurchaseDate)) AS DataStart,
               MAX(stg.ufn_TryParseDateTimeOffset(o.PurchaseDate)) AS DataEnd
        FROM stg.OrderLine AS o
        WHERE @BatchId IS NULL OR o.ImportBatchId = @BatchId
        GROUP BY o.ImportBatchId

        UNION ALL

        -- MIN/MAX ignore NULLs, so the header's period wins whenever the file has one.
        SELECT s.ImportBatchId,
               COALESCE(MIN(stg.ufn_TryParseDateTimeOffset(s.SettlementStartDate)), MIN(stg.ufn_TryParseDateTimeOffset(s.PostedDate))),
               COALESCE(MAX(stg.ufn_TryParseDateTimeOffset(s.SettlementEndDate)), MAX(stg.ufn_TryParseDateTimeOffset(s.PostedDate)))
        FROM stg.SettlementLine AS s
        WHERE @BatchId IS NULL OR s.ImportBatchId = @BatchId
        GROUP BY s.ImportBatchId

        UNION ALL

        SELECT i.ImportBatchId,
               CAST(MIN(TRY_CONVERT(date, i.SnapshotDate)) AS datetimeoffset),
               CAST(MAX(TRY_CONVERT(date, i.SnapshotDate)) AS datetimeoffset)
        FROM stg.InventoryRow AS i
        WHERE @BatchId IS NULL OR i.ImportBatchId = @BatchId
        GROUP BY i.ImportBatchId
    ) AS r ON r.BatchId = b.Id
    WHERE (@BatchId IS NULL OR b.Id = @BatchId)
      AND b.Source IN (N'Orders', N'Settlements', N'Inventory');

    -- Snapshot reports carry no dates; they are dated by when the batch was received.
    UPDATE stg.ImportBatch
    SET DataStart = UploadedAt,
        DataEnd = UploadedAt
    WHERE (@BatchId IS NULL OR Id = @BatchId)
      AND Source IN (N'FbaInventory', N'FbaReservedInventory', N'FbaRestockRecommendations', N'FbaInventoryPlanning');
END;
