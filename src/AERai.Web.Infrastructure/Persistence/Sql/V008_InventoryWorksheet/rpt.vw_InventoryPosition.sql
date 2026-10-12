/*
  rpt.vw_InventoryPosition  (V008: adds the SKU's color and Amazon's restock recommendation)
  Purpose : Each SKU's current stock in each marketplace, broken down by state, plus the units the
            seller holds outside Amazon, for the Inventory page, the dashboard, and restock planning.
  Grain   : One row per (MarketplaceId, Sku) that has a main inventory snapshot or home stock.
  Sources : core.InventorySnapshot, core.HomeStock, core.Product (ASIN, title, picture, color),
            core.ProductFamily, core.RestockRecommendation (Amazon's latest advice; NULL when none).
  Notes   : The snapshot date is the latest date with any state other than the reserved-reason
            states (ReservedCustomerOrder/FcTransfer/FcProcessing). Those come from a separate report
            that can land before the day's main snapshot; on their own they are not a full picture.
            A SKU held only at home (nothing at Amazon yet) has a NULL SnapshotDate and zero Amazon
            states, so it still shows up for planning. Unsplit Inbound/Reserved are exposed
            separately so callers can tell whether a breakdown exists. Sales velocity is computed by
            InventoryService, not here. Stock is never pooled across marketplaces.
*/
CREATE OR ALTER VIEW rpt.vw_InventoryPosition
AS
WITH LatestSnapshot AS (
    SELECT MarketplaceId, Sku, MAX(SnapshotDate) AS SnapshotDate
    FROM core.InventorySnapshot
    WHERE State NOT IN (N'ReservedCustomerOrder', N'ReservedFcTransfer', N'ReservedFcProcessing')
    GROUP BY MarketplaceId, Sku
),
Stock AS (
    SELECT
        l.MarketplaceId,
        l.Sku,
        l.SnapshotDate,
        SUM(CASE WHEN s.State = N'Available' THEN s.Quantity ELSE 0 END) AS Available,
        SUM(CASE WHEN s.State = N'InboundWorking' THEN s.Quantity ELSE 0 END) AS InboundWorking,
        SUM(CASE WHEN s.State = N'InboundShipped' THEN s.Quantity ELSE 0 END) AS InboundShipped,
        SUM(CASE WHEN s.State = N'InboundReceiving' THEN s.Quantity ELSE 0 END) AS InboundReceiving,
        SUM(CASE WHEN s.State = N'Inbound' THEN s.Quantity ELSE 0 END) AS InboundUnsplit,
        SUM(CASE WHEN s.State = N'ReservedCustomerOrder' THEN s.Quantity ELSE 0 END) AS ReservedCustomerOrder,
        SUM(CASE WHEN s.State = N'ReservedFcTransfer' THEN s.Quantity ELSE 0 END) AS ReservedFcTransfer,
        SUM(CASE WHEN s.State = N'ReservedFcProcessing' THEN s.Quantity ELSE 0 END) AS ReservedFcProcessing,
        SUM(CASE WHEN s.State = N'Reserved' THEN s.Quantity ELSE 0 END) AS ReservedUnsplit,
        SUM(CASE WHEN s.State = N'Unfulfillable' THEN s.Quantity ELSE 0 END) AS Unfulfillable
    FROM LatestSnapshot AS l
    INNER JOIN core.InventorySnapshot AS s
        ON s.MarketplaceId = l.MarketplaceId AND s.Sku = l.Sku AND s.SnapshotDate = l.SnapshotDate
    GROUP BY l.MarketplaceId, l.Sku, l.SnapshotDate
),
Keys AS (
    SELECT MarketplaceId, Sku FROM Stock
    UNION
    SELECT MarketplaceId, Sku FROM core.HomeStock
)
SELECT
    k.MarketplaceId,
    k.Sku,
    pr.Asin,
    pr.Title,
    pr.FamilyId,
    f.Name AS Family,
    pr.ImagePath,
    pr.Color,
    st.SnapshotDate,
    CAST(ISNULL(st.Available, 0) AS int) AS Available,
    CAST(ISNULL(st.InboundWorking, 0) AS int) AS InboundWorking,
    CAST(ISNULL(st.InboundShipped, 0) AS int) AS InboundShipped,
    CAST(ISNULL(st.InboundReceiving, 0) AS int) AS InboundReceiving,
    CAST(ISNULL(st.InboundUnsplit, 0) AS int) AS InboundUnsplit,
    CAST(ISNULL(st.ReservedCustomerOrder, 0) AS int) AS ReservedCustomerOrder,
    CAST(ISNULL(st.ReservedFcTransfer, 0) AS int) AS ReservedFcTransfer,
    CAST(ISNULL(st.ReservedFcProcessing, 0) AS int) AS ReservedFcProcessing,
    CAST(ISNULL(st.ReservedUnsplit, 0) AS int) AS ReservedUnsplit,
    CAST(ISNULL(st.Unfulfillable, 0) AS int) AS Unfulfillable,
    CAST(ISNULL(h.Quantity, 0) AS int) AS HomeStock,
    rr.RecommendedQuantity AS AmazonRecommendedQuantity,
    rr.RecommendedShipDate AS AmazonRecommendedShipDate
FROM Keys AS k
LEFT JOIN Stock AS st ON st.MarketplaceId = k.MarketplaceId AND st.Sku = k.Sku
LEFT JOIN core.HomeStock AS h ON h.MarketplaceId = k.MarketplaceId AND h.Sku = k.Sku
LEFT JOIN core.Product AS pr ON pr.Sku = k.Sku
LEFT JOIN core.ProductFamily AS f ON f.Id = pr.FamilyId
LEFT JOIN core.RestockRecommendation AS rr ON rr.MarketplaceId = k.MarketplaceId AND rr.Sku = k.Sku;
