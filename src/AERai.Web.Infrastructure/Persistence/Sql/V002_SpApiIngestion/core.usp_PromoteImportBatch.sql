/*
  core.usp_PromoteImportBatch  (V002: adds the FbaInventory source)
  Purpose      : Promotes one staging batch from stg.* into the curated core.* tables.
  Inputs       : @BatchId - stg.ImportBatch.Id.
  Side effects : Sets stg.*.ErrorMessage on rows that cannot be converted (they are skipped).
                 Upserts core.Product; upserts core.[Order]/core.OrderItem (Orders); replaces
                 core.InventorySnapshot rows per (Sku, SnapshotDate) (Inventory); upserts
                 core.Settlement and replaces its lines (Settlements); unpivots the wide FBA report
                 into per-state core.InventorySnapshot rows dated by the batch's UTC receive date
                 (FbaInventory). Updates the batch's status
                 and counts. On an unexpected error the transaction rolls back, the batch is marked
                 Failed, and the error is re-thrown.
  Idempotency  : Safe to run repeatedly for the same batch; results converge to the same state.
                 Within a batch, when the same natural key appears twice the LAST row in the file wins.
*/
CREATE OR ALTER PROCEDURE core.usp_PromoteImportBatch
    @BatchId bigint
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Source nvarchar(32);
    DECLARE @Now datetimeoffset = SYSDATETIMEOFFSET();
    DECLARE @Total int = 0;
    DECLARE @Rejected int = 0;

    SELECT @Source = Source FROM stg.ImportBatch WHERE Id = @BatchId;
    IF @Source IS NULL
        THROW 50001, N'Import batch not found.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        /* ------------------------------------------------------------------ Orders */
        IF @Source = N'Orders'
        BEGIN
            UPDATE stg.OrderLine
            SET ErrorMessage = CASE
                    WHEN AmazonOrderId IS NULL THEN N'Missing amazon-order-id.'
                    WHEN LEN(AmazonOrderId) > 32 THEN N'amazon-order-id is longer than 32 characters.'
                    WHEN Sku IS NULL THEN N'Missing sku.'
                    WHEN LEN(Sku) > 64 THEN N'sku is longer than 64 characters.'
                    WHEN OrderStatus IS NULL THEN N'Missing order-status.'
                    WHEN stg.ufn_TryParseDateTimeOffset(PurchaseDate) IS NULL
                        THEN CONCAT(N'Invalid purchase-date ''', PurchaseDate, N'''.')
                    WHEN TRY_CONVERT(int, Quantity) IS NULL OR TRY_CONVERT(int, Quantity) < 0
                        THEN CONCAT(N'Invalid quantity ''', Quantity, N'''.')
                    -- A blank price is valid (pending orders have no price yet) and promotes as 0.
                    WHEN ItemPrice IS NOT NULL AND TRY_CONVERT(money, ItemPrice) IS NULL
                        THEN CONCAT(N'Invalid item-price ''', ItemPrice, N'''.')
                    ELSE NULL
                END
            WHERE ImportBatchId = @BatchId;

            SELECT AmazonOrderId, Sku, Asin, ProductName, OrderStatus, Currency, PurchaseDate, Quantity, ItemPrice, RowNumber
            INTO #OrderLines
            FROM (
                SELECT
                    AmazonOrderId,
                    Sku,
                    Asin,
                    ProductName,
                    LEFT(OrderStatus, 32) AS OrderStatus,
                    LEFT(UPPER(Currency), 3) AS Currency,
                    stg.ufn_TryParseDateTimeOffset(PurchaseDate) AS PurchaseDate,
                    TRY_CONVERT(int, Quantity) AS Quantity,
                    ISNULL(CAST(TRY_CONVERT(money, ItemPrice) AS decimal(18, 2)), 0) AS ItemPrice,
                    RowNumber,
                    ROW_NUMBER() OVER (PARTITION BY AmazonOrderId, Sku ORDER BY RowNumber DESC) AS Recency
                FROM stg.OrderLine
                WHERE ImportBatchId = @BatchId AND ErrorMessage IS NULL
            ) AS ranked
            WHERE Recency = 1;

            MERGE core.Product AS target
            USING (
                SELECT Sku, LEFT(MAX(Asin), 16) AS Asin, MAX(ProductName) AS Title
                FROM #OrderLines
                GROUP BY Sku
            ) AS source
            ON target.Sku = source.Sku
            WHEN MATCHED THEN
                UPDATE SET Asin = COALESCE(source.Asin, target.Asin),
                           Title = COALESCE(source.Title, target.Title),
                           UpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (Sku, Asin, Title, CreatedAt, UpdatedAt)
                VALUES (source.Sku, source.Asin, source.Title, @Now, @Now);

            -- Order-level attributes come from the order's last line in the file.
            MERGE core.[Order] AS target
            USING (
                SELECT AmazonOrderId, PurchaseDate, OrderStatus, Currency
                FROM (
                    SELECT AmazonOrderId, PurchaseDate, OrderStatus, Currency,
                           ROW_NUMBER() OVER (PARTITION BY AmazonOrderId ORDER BY RowNumber DESC) AS Recency
                    FROM #OrderLines
                ) AS ranked
                WHERE Recency = 1
            ) AS source
            ON target.AmazonOrderId = source.AmazonOrderId
            WHEN MATCHED THEN
                UPDATE SET PurchaseDate = source.PurchaseDate,
                           OrderStatus = source.OrderStatus,
                           Currency = COALESCE(source.Currency, target.Currency),
                           LastImportBatchId = @BatchId,
                           UpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (AmazonOrderId, PurchaseDate, OrderStatus, Currency, LastImportBatchId, UpdatedAt)
                VALUES (source.AmazonOrderId, source.PurchaseDate, source.OrderStatus, source.Currency, @BatchId, @Now);

            MERGE core.OrderItem AS target
            USING (
                SELECT o.Id AS OrderId, l.Sku, l.Quantity, l.ItemPrice
                FROM #OrderLines AS l
                INNER JOIN core.[Order] AS o ON o.AmazonOrderId = l.AmazonOrderId
            ) AS source
            ON target.OrderId = source.OrderId AND target.Sku = source.Sku
            WHEN MATCHED THEN
                UPDATE SET Quantity = source.Quantity, ItemPrice = source.ItemPrice
            WHEN NOT MATCHED THEN
                INSERT (OrderId, Sku, Quantity, ItemPrice)
                VALUES (source.OrderId, source.Sku, source.Quantity, source.ItemPrice);

            SELECT @Total = COUNT(*), @Rejected = COUNT(ErrorMessage)
            FROM stg.OrderLine WHERE ImportBatchId = @BatchId;
        END

        /* --------------------------------------------------------------- Inventory */
        ELSE IF @Source = N'Inventory'
        BEGIN
            UPDATE stg.InventoryRow
            SET ErrorMessage = CASE
                    WHEN Sku IS NULL THEN N'Missing sku.'
                    WHEN LEN(Sku) > 64 THEN N'sku is longer than 64 characters.'
                    WHEN TRY_CONVERT(date, SnapshotDate) IS NULL
                        THEN CONCAT(N'Invalid snapshot-date ''', SnapshotDate, N'''.')
                    WHEN stg.ufn_NormalizeInventoryState(State) IS NULL
                        THEN CONCAT(N'Unrecognized state ''', State, N'''.')
                    WHEN TRY_CONVERT(int, Quantity) IS NULL OR TRY_CONVERT(int, Quantity) < 0
                        THEN CONCAT(N'Invalid quantity ''', Quantity, N'''.')
                    ELSE NULL
                END
            WHERE ImportBatchId = @BatchId;

            -- De-duplicate on the RAW state first (last row wins), then sum into canonical states,
            -- because several raw labels (e.g. inbound-working/-shipped/-receiving) roll up to one.
            SELECT SnapshotDate, Sku, State, SUM(Quantity) AS Quantity
            INTO #Inventory
            FROM (
                SELECT
                    TRY_CONVERT(date, SnapshotDate) AS SnapshotDate,
                    Sku,
                    stg.ufn_NormalizeInventoryState(State) AS State,
                    TRY_CONVERT(int, Quantity) AS Quantity,
                    ROW_NUMBER() OVER (
                        PARTITION BY TRY_CONVERT(date, SnapshotDate), Sku, LOWER(State)
                        ORDER BY RowNumber DESC) AS Recency
                FROM stg.InventoryRow
                WHERE ImportBatchId = @BatchId AND ErrorMessage IS NULL
            ) AS ranked
            WHERE Recency = 1
            GROUP BY SnapshotDate, Sku, State;

            MERGE core.Product AS target
            USING (
                SELECT Sku, LEFT(MAX(Asin), 16) AS Asin, MAX(ProductName) AS Title
                FROM stg.InventoryRow
                WHERE ImportBatchId = @BatchId AND ErrorMessage IS NULL
                GROUP BY Sku
            ) AS source
            ON target.Sku = source.Sku
            WHEN MATCHED THEN
                UPDATE SET Asin = COALESCE(source.Asin, target.Asin),
                           Title = COALESCE(source.Title, target.Title),
                           UpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (Sku, Asin, Title, CreatedAt, UpdatedAt)
                VALUES (source.Sku, source.Asin, source.Title, @Now, @Now);

            -- A snapshot is the complete picture for a SKU on a date: states missing from the file
            -- mean zero, so replace the whole (Sku, SnapshotDate) set rather than merging states.
            DELETE target
            FROM core.InventorySnapshot AS target
            WHERE EXISTS (
                SELECT 1 FROM #Inventory AS i
                WHERE i.Sku = target.Sku AND i.SnapshotDate = target.SnapshotDate);

            INSERT core.InventorySnapshot (SnapshotDate, Sku, State, Quantity, LastImportBatchId)
            SELECT SnapshotDate, Sku, State, Quantity, @BatchId
            FROM #Inventory;

            SELECT @Total = COUNT(*), @Rejected = COUNT(ErrorMessage)
            FROM stg.InventoryRow WHERE ImportBatchId = @BatchId;
        END

        /* ------------------------------------------------------------- Settlements */
        ELSE IF @Source = N'Settlements'
        BEGIN
            -- The first row of a settlement file is a header (period dates, no transaction or
            -- amount type). It is valid and used for the settlement period, but is not a line.
            UPDATE stg.SettlementLine
            SET ErrorMessage = CASE
                    WHEN SettlementId IS NULL THEN N'Missing settlement-id.'
                    WHEN LEN(SettlementId) > 32 THEN N'settlement-id is longer than 32 characters.'
                    WHEN TransactionType IS NULL AND AmountType IS NULL THEN NULL
                    WHEN TransactionType IS NULL THEN N'Missing transaction-type.'
                    WHEN TRY_CONVERT(money, Amount) IS NULL
                        THEN CONCAT(N'Invalid amount ''', Amount, N'''.')
                    ELSE NULL
                END
            WHERE ImportBatchId = @BatchId;

            MERGE core.Settlement AS target
            USING (
                SELECT
                    SettlementId,
                    MIN(stg.ufn_TryParseDateTimeOffset(SettlementStartDate)) AS PeriodStart,
                    MAX(stg.ufn_TryParseDateTimeOffset(SettlementEndDate)) AS PeriodEnd,
                    LEFT(MAX(UPPER(Currency)), 3) AS Currency
                FROM stg.SettlementLine
                WHERE ImportBatchId = @BatchId AND ErrorMessage IS NULL
                GROUP BY SettlementId
            ) AS source
            ON target.SettlementId = source.SettlementId
            WHEN MATCHED THEN
                UPDATE SET PeriodStart = COALESCE(source.PeriodStart, target.PeriodStart),
                           PeriodEnd = COALESCE(source.PeriodEnd, target.PeriodEnd),
                           Currency = COALESCE(source.Currency, target.Currency),
                           LastImportBatchId = @BatchId
            WHEN NOT MATCHED THEN
                INSERT (SettlementId, PeriodStart, PeriodEnd, Currency, LastImportBatchId)
                VALUES (source.SettlementId, source.PeriodStart, source.PeriodEnd, source.Currency, @BatchId);

            -- Settlement lines have no natural key, so a settlement's lines are replaced as a unit.
            DELETE target
            FROM core.SettlementLine AS target
            WHERE target.SettlementId IN (
                SELECT SettlementId FROM stg.SettlementLine
                WHERE ImportBatchId = @BatchId AND ErrorMessage IS NULL);

            INSERT core.SettlementLine
                (SettlementId, PostedDate, TransactionType, AmazonOrderId, Sku, AmountType, AmountDescription, Amount)
            SELECT
                SettlementId,
                stg.ufn_TryParseDateTimeOffset(PostedDate),
                LEFT(TransactionType, 64),
                LEFT(OrderId, 32),
                LEFT(Sku, 64),
                LEFT(AmountType, 64),
                LEFT(AmountDescription, 128),
                CAST(TRY_CONVERT(money, Amount) AS decimal(18, 2))
            FROM stg.SettlementLine
            WHERE ImportBatchId = @BatchId AND ErrorMessage IS NULL AND TransactionType IS NOT NULL;

            SELECT @Total = COUNT(*), @Rejected = COUNT(ErrorMessage)
            FROM stg.SettlementLine WHERE ImportBatchId = @BatchId;
        END
        /* ------------------------------------------------------------ FbaInventory */
        ELSE IF @Source = N'FbaInventory'
        BEGIN
            -- The report has no date column: it is a snapshot as of when it was received.
            DECLARE @SnapshotDate date =
                (SELECT CAST(SWITCHOFFSET(UploadedAt, '+00:00') AS date) FROM stg.ImportBatch WHERE Id = @BatchId);

            UPDATE stg.FbaInventoryRow
            SET ErrorMessage = CASE
                    WHEN Sku IS NULL THEN N'Missing sku.'
                    WHEN LEN(Sku) > 64 THEN N'sku is longer than 64 characters.'
                    WHEN TRY_CONVERT(int, AfnFulfillableQuantity) IS NULL OR TRY_CONVERT(int, AfnFulfillableQuantity) < 0
                        THEN CONCAT(N'Invalid afn-fulfillable-quantity ''', AfnFulfillableQuantity, N'''.')
                    -- The remaining quantity columns are optional, but must be whole numbers when present.
                    WHEN AfnUnsellableQuantity IS NOT NULL AND TRY_CONVERT(int, AfnUnsellableQuantity) IS NULL
                        THEN CONCAT(N'Invalid afn-unsellable-quantity ''', AfnUnsellableQuantity, N'''.')
                    WHEN AfnReservedQuantity IS NOT NULL AND TRY_CONVERT(int, AfnReservedQuantity) IS NULL
                        THEN CONCAT(N'Invalid afn-reserved-quantity ''', AfnReservedQuantity, N'''.')
                    WHEN AfnInboundWorkingQuantity IS NOT NULL AND TRY_CONVERT(int, AfnInboundWorkingQuantity) IS NULL
                        THEN CONCAT(N'Invalid afn-inbound-working-quantity ''', AfnInboundWorkingQuantity, N'''.')
                    WHEN AfnInboundShippedQuantity IS NOT NULL AND TRY_CONVERT(int, AfnInboundShippedQuantity) IS NULL
                        THEN CONCAT(N'Invalid afn-inbound-shipped-quantity ''', AfnInboundShippedQuantity, N'''.')
                    WHEN AfnInboundReceivingQuantity IS NOT NULL AND TRY_CONVERT(int, AfnInboundReceivingQuantity) IS NULL
                        THEN CONCAT(N'Invalid afn-inbound-receiving-quantity ''', AfnInboundReceivingQuantity, N'''.')
                    ELSE NULL
                END
            WHERE ImportBatchId = @BatchId;

            -- Unpivot one wide row per SKU into canonical states; the three inbound columns sum into Inbound.
            SELECT @SnapshotDate AS SnapshotDate, r.Sku, v.State, SUM(v.Quantity) AS Quantity
            INTO #Fba
            FROM (
                SELECT *, ROW_NUMBER() OVER (PARTITION BY Sku ORDER BY RowNumber DESC) AS Recency
                FROM stg.FbaInventoryRow
                WHERE ImportBatchId = @BatchId AND ErrorMessage IS NULL
            ) AS r
            CROSS APPLY (VALUES
                (N'Available', TRY_CONVERT(int, r.AfnFulfillableQuantity)),
                (N'Unfulfillable', TRY_CONVERT(int, r.AfnUnsellableQuantity)),
                (N'Reserved', TRY_CONVERT(int, r.AfnReservedQuantity)),
                (N'Inbound', TRY_CONVERT(int, r.AfnInboundWorkingQuantity)),
                (N'Inbound', TRY_CONVERT(int, r.AfnInboundShippedQuantity)),
                (N'Inbound', TRY_CONVERT(int, r.AfnInboundReceivingQuantity))
            ) AS v (State, Quantity)
            WHERE r.Recency = 1 AND v.Quantity IS NOT NULL
            GROUP BY r.Sku, v.State;

            MERGE core.Product AS target
            USING (
                SELECT Sku, LEFT(MAX(Asin), 16) AS Asin, MAX(ProductName) AS Title
                FROM stg.FbaInventoryRow
                WHERE ImportBatchId = @BatchId AND ErrorMessage IS NULL
                GROUP BY Sku
            ) AS source
            ON target.Sku = source.Sku
            WHEN MATCHED THEN
                UPDATE SET Asin = COALESCE(source.Asin, target.Asin),
                           Title = COALESCE(source.Title, target.Title),
                           UpdatedAt = @Now
            WHEN NOT MATCHED THEN
                INSERT (Sku, Asin, Title, CreatedAt, UpdatedAt)
                VALUES (source.Sku, source.Asin, source.Title, @Now, @Now);

            -- Same replacement rule as Inventory: a snapshot is the complete picture for (Sku, date).
            DELETE target
            FROM core.InventorySnapshot AS target
            WHERE EXISTS (
                SELECT 1 FROM #Fba AS f
                WHERE f.Sku = target.Sku AND f.SnapshotDate = target.SnapshotDate);

            INSERT core.InventorySnapshot (SnapshotDate, Sku, State, Quantity, LastImportBatchId)
            SELECT SnapshotDate, Sku, State, Quantity, @BatchId
            FROM #Fba;

            SELECT @Total = COUNT(*), @Rejected = COUNT(ErrorMessage)
            FROM stg.FbaInventoryRow WHERE ImportBatchId = @BatchId;
        END
        ELSE
            THROW 50002, N'Unsupported import source.', 1;

        UPDATE stg.ImportBatch
        SET Status = CASE WHEN @Rejected = 0 THEN N'Promoted' ELSE N'PromotedWithErrors' END,
            PromotedRowCount = @Total - @Rejected,
            RejectedRowCount = @Rejected,
            PromotedAt = @Now,
            ErrorMessage = NULL
        WHERE Id = @BatchId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        -- Recorded outside the rolled-back transaction so the failure is visible in the Batches tool.
        UPDATE stg.ImportBatch
        SET Status = N'Failed', ErrorMessage = LEFT(ERROR_MESSAGE(), 2000)
        WHERE Id = @BatchId;

        THROW;
    END CATCH
END;
