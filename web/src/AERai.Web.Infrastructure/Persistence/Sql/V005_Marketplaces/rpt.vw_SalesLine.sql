/*
  rpt.vw_SalesLine  (V005: adds MarketplaceId)
  Purpose : Sold order items with exact purchase timestamps, for the dashboard. Day/hour bucketing
            happens in the application so it can use the marketplace's local time zone rather than UTC.
  Grain   : One row per order item.
  Sources : core.[Order], core.OrderItem, core.Product (title only).
  Notes   : Cancelled orders are excluded (both spellings appear in Amazon data). Pending orders are
            included, matching Amazon's "ordered product sales". Callers always filter on one
            MarketplaceId and a bounded PurchaseDate window.
*/
CREATE OR ALTER VIEW rpt.vw_SalesLine
AS
SELECT
    o.MarketplaceId,
    o.AmazonOrderId,
    o.PurchaseDate,
    o.OrderStatus,
    o.Currency,
    i.Sku,
    p.Title,
    i.Quantity,
    i.ItemPrice
FROM core.[Order] AS o
INNER JOIN core.OrderItem AS i ON i.OrderId = o.Id
LEFT JOIN core.Product AS p ON p.Sku = i.Sku
WHERE o.OrderStatus NOT IN (N'Cancelled', N'Canceled');
