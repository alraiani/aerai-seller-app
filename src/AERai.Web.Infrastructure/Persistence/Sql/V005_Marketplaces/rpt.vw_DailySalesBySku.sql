/*
  rpt.vw_DailySalesBySku  (V005: adds MarketplaceId)
  Purpose : Units and revenue per SKU per purchase day in each marketplace, for sales reporting.
  Grain   : One row per (MarketplaceId, SalesDate, Sku).
  Sources : core.[Order], core.OrderItem, core.Product (title only).
  Notes   : SalesDate is the UTC calendar date of the purchase (core.[Order].PurchaseDateUtc).
            Cancelled orders are excluded (both spellings appear in Amazon data). Revenue is in the
            marketplace's currency, so rows from different marketplaces must never be summed.
*/
CREATE OR ALTER VIEW rpt.vw_DailySalesBySku
AS
SELECT
    o.MarketplaceId,
    o.PurchaseDateUtc AS SalesDate,
    i.Sku,
    p.Title,
    CAST(COUNT(DISTINCT o.Id) AS int) AS OrderCount,
    CAST(SUM(i.Quantity) AS int) AS Units,
    CAST(SUM(i.ItemPrice) AS decimal(18, 2)) AS Revenue
FROM core.[Order] AS o
INNER JOIN core.OrderItem AS i ON i.OrderId = o.Id
LEFT JOIN core.Product AS p ON p.Sku = i.Sku
WHERE o.OrderStatus NOT IN (N'Cancelled', N'Canceled')
GROUP BY o.MarketplaceId, o.PurchaseDateUtc, i.Sku, p.Title;
