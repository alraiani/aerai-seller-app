/*
  backfill-settlement-units  (V015)
  Purpose      : Gives settlements imported before V014 their order-item-code and quantity-purchased,
                 so units sold and price per item appear without re-importing the raw files.
  Inputs       : stg.SettlementLine.RawLine (the original tab-separated file line, kept for every row).
  Side effects : Fills stg.SettlementLine.OrderItemCode and QuantityPurchased from the raw line, then
                 re-promotes each affected settlement's latest batch (core.usp_PromoteImportBatch replaces
                 that settlement's core lines, now with the two new columns).
  Idempotency  : Only rows with both columns still empty are filled, and only settlements that have no
                 quantity in core yet are re-promoted, so running it again (or on a database that was
                 already refreshed) does nothing.
  Notes        : The columns are read by position, which is fixed in the settlement flat file V2
                 (order-item-code is field 19, quantity-purchased field 23). A row is only filled when
                 its sku (field 22) and amount (field 15) match what was staged from the same line, so a
                 file with a different layout is left alone instead of being misread.
*/
SET NOCOUNT ON;

-- Each raw line is split once, then the wanted fields are picked out by position.
UPDATE l
SET OrderItemCode = NULLIF(LTRIM(RTRIM(p.OrderItemCode)), N''),
    QuantityPurchased = NULLIF(LTRIM(RTRIM(p.Quantity)), N'')
FROM stg.SettlementLine AS l
INNER JOIN (
    SELECT
        r.Id,
        MAX(CASE WHEN f.ordinal = 19 THEN f.value END) AS OrderItemCode,
        MAX(CASE WHEN f.ordinal = 23 THEN f.value END) AS Quantity,
        MAX(CASE WHEN f.ordinal = 22 THEN f.value END) AS Sku,
        MAX(CASE WHEN f.ordinal = 15 THEN f.value END) AS Amount
    FROM stg.SettlementLine AS r
    CROSS APPLY STRING_SPLIT(r.RawLine, CHAR(9), 1) AS f
    WHERE r.TransactionType IS NOT NULL AND r.OrderItemCode IS NULL AND r.QuantityPurchased IS NULL
    GROUP BY r.Id
) AS p ON p.Id = l.Id
WHERE ISNULL(p.Sku, N'') = ISNULL(l.Sku, N'')
  AND ISNULL(p.Amount, N'') = ISNULL(l.Amount, N'');

DECLARE @BatchId bigint;
DECLARE batches CURSOR LOCAL FAST_FORWARD FOR
    SELECT DISTINCT s.LastImportBatchId
    FROM core.Settlement AS s
    WHERE NOT EXISTS (SELECT 1 FROM core.SettlementLine AS cl WHERE cl.SettlementId = s.SettlementId AND cl.Quantity IS NOT NULL)
      AND EXISTS (SELECT 1 FROM stg.SettlementLine AS sl WHERE sl.ImportBatchId = s.LastImportBatchId AND sl.QuantityPurchased IS NOT NULL)
    ORDER BY s.LastImportBatchId;

OPEN batches;
FETCH NEXT FROM batches INTO @BatchId;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC core.usp_PromoteImportBatch @BatchId = @BatchId;
    FETCH NEXT FROM batches INTO @BatchId;
END;
CLOSE batches;
DEALLOCATE batches;
