/*
  backfill-pending-sends  (V016)
  Purpose      : Decides, for sends to Amazon logged before pending sends were tracked, which are still
                 pending and gives those the baseline the app would have stored when they were logged.
  Inputs       : core.HomeStockMovement (Shipped to Amazon entries), rpt.vw_InventoryPosition (V016:
                 each SKU's latest FBA snapshot and when it was received).
  Side effects : A send logged at or before its SKU's latest snapshot was received is closed (that
                 snapshot already reflects it, or it is old enough that Amazon has long caught up). A send
                 logged after it can't be in it yet: it stays open, with AmazonBaselineUnits = the
                 snapshot's sell-through stock (available + inbound + FC transfer + FC processing) and
                 AmazonBaselineAt = its receive time, exactly what the app stores on a new send. A SKU with
                 no snapshot at all has nothing at Amazon, so its sends stay open with a baseline of 0.
  Idempotency  : Only sends with no baseline and not yet closed are touched, and every send leaves with
                 one or the other, so running it again does nothing.
  Notes        : Runs after the V016 view is created, which it reads.
*/
SET NOCOUNT ON;

UPDATE m
SET ReconciledUnits = -m.Units, ReconciledAt = SYSDATETIMEOFFSET(), ReconciledBy = N'system'
FROM core.HomeStockMovement AS m
INNER JOIN rpt.vw_InventoryPosition AS p ON p.MarketplaceId = m.MarketplaceId AND p.Sku = m.Sku
WHERE m.Type = N'ShippedToAmazon' AND m.Units < 0
  AND m.AmazonBaselineUnits IS NULL AND m.ReconciledAt IS NULL
  AND p.SnapshotReceivedAt IS NOT NULL AND m.CreatedAt <= p.SnapshotReceivedAt;

UPDATE m
SET AmazonBaselineUnits = ISNULL(p.Available + p.InboundWorking + p.InboundShipped + p.InboundReceiving + p.InboundUnsplit
                                 + p.ReservedFcTransfer + p.ReservedFcProcessing, 0),
    AmazonBaselineAt = p.SnapshotReceivedAt
FROM core.HomeStockMovement AS m
LEFT JOIN rpt.vw_InventoryPosition AS p ON p.MarketplaceId = m.MarketplaceId AND p.Sku = m.Sku
WHERE m.Type = N'ShippedToAmazon' AND m.Units < 0
  AND m.AmazonBaselineUnits IS NULL AND m.ReconciledAt IS NULL;
