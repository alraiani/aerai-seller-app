/*
  rpt.vw_InventoryPosition  (V005: per marketplace)
  Purpose : Each SKU's current stock by state plus sales velocity in each marketplace, for the
            Inventory page and the dashboard's at-risk list.
  Grain   : One row per (MarketplaceId, Sku) that has at least one inventory snapshot.
  Sources : core.InventorySnapshot (latest SnapshotDate per marketplace and SKU), core.[Order]/
            core.OrderItem (same marketplace, trailing 30 UTC days including today, cancelled orders
            excluded), core.Product.
  Notes   : Each marketplace has its own fulfillment network, so stock and velocity are never pooled
            across marketplaces. DaysOfSupply = (Available + Inbound) / (UnitsSold30d / 30); NULL when
            nothing sold. Depends on the current date, so results change daily without new imports.
*/
CREATE OR ALTER VIEW rpt.vw_InventoryPosition
AS
WITH LatestSnapshot AS (
    SELECT MarketplaceId, Sku, MAX(SnapshotDate) AS SnapshotDate
    FROM core.InventorySnapshot
    GROUP BY MarketplaceId, Sku
),
Position AS (
    SELECT
        s.MarketplaceId,
        s.Sku,
        s.SnapshotDate,
        SUM(CASE WHEN s.State = N'Available' THEN s.Quantity ELSE 0 END) AS Available,
        SUM(CASE WHEN s.State = N'Inbound' THEN s.Quantity ELSE 0 END) AS Inbound,
        SUM(CASE WHEN s.State = N'Reserved' THEN s.Quantity ELSE 0 END) AS Reserved,
        SUM(CASE WHEN s.State = N'Unfulfillable' THEN s.Quantity ELSE 0 END) AS Unfulfillable
    FROM core.InventorySnapshot AS s
    INNER JOIN LatestSnapshot AS l
        ON l.MarketplaceId = s.MarketplaceId AND l.Sku = s.Sku AND l.SnapshotDate = s.SnapshotDate
    GROUP BY s.MarketplaceId, s.Sku, s.SnapshotDate
),
Sales AS (
    SELECT o.MarketplaceId, i.Sku, SUM(i.Quantity) AS UnitsSold30d
    FROM core.OrderItem AS i
    INNER JOIN core.[Order] AS o ON o.Id = i.OrderId
    WHERE o.OrderStatus NOT IN (N'Cancelled', N'Canceled')
      AND o.PurchaseDateUtc > DATEADD(day, -30, CAST(SYSUTCDATETIME() AS date))
    GROUP BY o.MarketplaceId, i.Sku
)
SELECT
    p.MarketplaceId,
    p.Sku,
    pr.Title,
    p.SnapshotDate,
    p.Available,
    p.Inbound,
    p.Reserved,
    p.Unfulfillable,
    CAST(ISNULL(s.UnitsSold30d, 0) AS int) AS UnitsSold30d,
    CAST(ISNULL(s.UnitsSold30d, 0) / 30.0 AS decimal(18, 2)) AS DailyVelocity,
    CAST(CASE
            WHEN ISNULL(s.UnitsSold30d, 0) = 0 THEN NULL
            ELSE (p.Available + p.Inbound) / (s.UnitsSold30d / 30.0)
         END AS decimal(18, 1)) AS DaysOfSupply
FROM Position AS p
LEFT JOIN core.Product AS pr ON pr.Sku = p.Sku
LEFT JOIN Sales AS s ON s.MarketplaceId = p.MarketplaceId AND s.Sku = p.Sku;
