/*
  rpt.vw_OrderSummary
  Purpose : Order list with item totals, for the Orders page.
  Grain   : One row per order (AmazonOrderId).
  Sources : core.[Order], core.OrderItem.
  Notes   : Includes cancelled orders (the page shows status); orders without items show zeros.
*/
CREATE OR ALTER VIEW rpt.vw_OrderSummary
AS
SELECT
    o.AmazonOrderId,
    o.PurchaseDate,
    o.OrderStatus,
    o.Currency,
    CAST(COUNT(i.Id) AS int) AS LineCount,
    CAST(ISNULL(SUM(i.Quantity), 0) AS int) AS Units,
    CAST(ISNULL(SUM(i.ItemPrice), 0) AS decimal(18, 2)) AS OrderTotal
FROM core.[Order] AS o
LEFT JOIN core.OrderItem AS i ON i.OrderId = o.Id
GROUP BY o.AmazonOrderId, o.PurchaseDate, o.OrderStatus, o.Currency;
