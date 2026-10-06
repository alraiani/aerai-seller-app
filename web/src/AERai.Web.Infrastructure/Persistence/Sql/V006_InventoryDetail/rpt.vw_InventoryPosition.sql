/*
  rpt.vw_InventoryPosition  (V006: one column per inventory state; velocity moves to the application)
  Purpose : Each SKU's current stock in each marketplace, broken down by state, for the Inventory
            page, the dashboard, and restock planning.
  Grain   : One row per (MarketplaceId, Sku) that has a main inventory snapshot.
  Sources : core.InventorySnapshot, core.Product (ASIN and title).
  Notes   : The snapshot date is the latest date with any state other than the reserved-reason
            states (ReservedCustomerOrder/FcTransfer/FcProcessing). Those come from a separate report
            that can land before the day's main snapshot; on their own they are not a full picture.
            Unsplit Inbound/Reserved are exposed separately so callers can tell whether a breakdown
            exists. Sales velocity and days of supply are computed by InventoryService (local
            business days, 90-day window), not here. Stock is never pooled across marketplaces.
*/
CREATE OR ALTER VIEW rpt.vw_InventoryPosition
AS
WITH LatestSnapshot AS (
    SELECT MarketplaceId, Sku, MAX(SnapshotDate) AS SnapshotDate
    FROM core.InventorySnapshot
    WHERE State NOT IN (N'ReservedCustomerOrder', N'ReservedFcTransfer', N'ReservedFcProcessing')
    GROUP BY MarketplaceId, Sku
)
SELECT
    l.MarketplaceId,
    l.Sku,
    pr.Asin,
    pr.Title,
    l.SnapshotDate,
    CAST(SUM(CASE WHEN s.State = N'Available' THEN s.Quantity ELSE 0 END) AS int) AS Available,
    CAST(SUM(CASE WHEN s.State = N'InboundWorking' THEN s.Quantity ELSE 0 END) AS int) AS InboundWorking,
    CAST(SUM(CASE WHEN s.State = N'InboundShipped' THEN s.Quantity ELSE 0 END) AS int) AS InboundShipped,
    CAST(SUM(CASE WHEN s.State = N'InboundReceiving' THEN s.Quantity ELSE 0 END) AS int) AS InboundReceiving,
    CAST(SUM(CASE WHEN s.State = N'Inbound' THEN s.Quantity ELSE 0 END) AS int) AS InboundUnsplit,
    CAST(SUM(CASE WHEN s.State = N'ReservedCustomerOrder' THEN s.Quantity ELSE 0 END) AS int) AS ReservedCustomerOrder,
    CAST(SUM(CASE WHEN s.State = N'ReservedFcTransfer' THEN s.Quantity ELSE 0 END) AS int) AS ReservedFcTransfer,
    CAST(SUM(CASE WHEN s.State = N'ReservedFcProcessing' THEN s.Quantity ELSE 0 END) AS int) AS ReservedFcProcessing,
    CAST(SUM(CASE WHEN s.State = N'Reserved' THEN s.Quantity ELSE 0 END) AS int) AS ReservedUnsplit,
    CAST(SUM(CASE WHEN s.State = N'Unfulfillable' THEN s.Quantity ELSE 0 END) AS int) AS Unfulfillable
FROM LatestSnapshot AS l
INNER JOIN core.InventorySnapshot AS s
    ON s.MarketplaceId = l.MarketplaceId AND s.Sku = l.Sku AND s.SnapshotDate = l.SnapshotDate
LEFT JOIN core.Product AS pr ON pr.Sku = l.Sku
GROUP BY l.MarketplaceId, l.Sku, pr.Asin, pr.Title, l.SnapshotDate;
