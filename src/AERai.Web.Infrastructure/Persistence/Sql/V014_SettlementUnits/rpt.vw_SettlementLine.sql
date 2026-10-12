/*
  rpt.vw_SettlementLine  (V014: price per item and units)
  Purpose : Every settlement line with the bucket it counts toward and the SKU/price group it belongs
            to. The single definition of the Sales/Fees/Refunds/Other rule, so the settlement list, the
            per-SKU summary, and the line-level drill-down always add up to the same payout.
  Grain   : One row per core.SettlementLine.
  Sources : core.Settlement, core.SettlementLine.
  Notes   : Buckets, in priority order:
              Refunds - transaction-type 'Refund' (all of its amounts, including refunded fees)
              Sales   - transaction-type 'Order' with amount-type 'ItemPrice'
              Fees    - amount-type 'ItemFees', or transaction-type 'ServiceFee'
              Other   - everything else (promotions, reimbursements, adjustments)
            SalePrice is the price per item: the Principal of the line's order item (settlement, order,
            order-item-code, SKU, transaction type) divided by its quantity-purchased. Without a quantity
            (refund lines, or lines imported before it was kept) it is the whole Principal. Orders and
            refunds are summed apart so a refund does not cancel the sale it reverses. It is NULL for
            lines with no SKU or no Principal (e.g. service fees).
            Units is the quantity on the order item's Principal line and 0 on every other line, so
            summing it counts each order item once. Refund lines carry no quantity, so refunds never
            reduce Units.
*/
CREATE OR ALTER VIEW rpt.vw_SettlementLine
AS
WITH Lines AS (
    SELECT
        s.MarketplaceId,
        s.SettlementId,
        s.PeriodStart,
        s.PeriodEnd,
        s.Currency,
        l.Id AS LineId,
        l.PostedDate,
        l.TransactionType,
        l.AmazonOrderId,
        l.Sku,
        l.AmountType,
        l.AmountDescription,
        l.Amount,
        b.Bucket,
        p.IsPrincipal,
        CASE WHEN p.IsPrincipal = 1 AND l.TransactionType = N'Order' THEN ISNULL(l.Quantity, 0) ELSE 0 END AS Units,
        SUM(CASE WHEN p.IsPrincipal = 1 THEN l.Amount END)
            OVER (PARTITION BY l.SettlementId, ISNULL(l.AmazonOrderId, N''), ISNULL(l.OrderItemCode, N''), l.Sku, l.TransactionType) AS ItemPrincipal,
        SUM(CASE WHEN p.IsPrincipal = 1 THEN l.Quantity END)
            OVER (PARTITION BY l.SettlementId, ISNULL(l.AmazonOrderId, N''), ISNULL(l.OrderItemCode, N''), l.Sku, l.TransactionType) AS ItemQuantity
    FROM core.Settlement AS s
    INNER JOIN core.SettlementLine AS l ON l.SettlementId = s.SettlementId
    CROSS APPLY (
        SELECT CASE
            WHEN l.TransactionType = N'Refund' THEN N'Refunds'
            WHEN l.TransactionType = N'Order' AND l.AmountType = N'ItemPrice' THEN N'Sales'
            WHEN l.AmountType = N'ItemFees' OR l.TransactionType = N'ServiceFee' THEN N'Fees'
            ELSE N'Other'
        END AS Bucket
    ) AS b
    CROSS APPLY (
        SELECT CASE WHEN l.AmountType = N'ItemPrice' AND l.AmountDescription = N'Principal' THEN 1 ELSE 0 END AS IsPrincipal
    ) AS p
)
SELECT
    MarketplaceId,
    SettlementId,
    PeriodStart,
    PeriodEnd,
    Currency,
    LineId,
    PostedDate,
    TransactionType,
    AmazonOrderId,
    Sku,
    AmountType,
    AmountDescription,
    Amount,
    Bucket,
    Units,
    CAST(CASE WHEN Sku IS NULL OR ItemPrincipal IS NULL THEN NULL
              ELSE ABS(ItemPrincipal) / CASE WHEN ItemQuantity > 0 THEN ItemQuantity ELSE 1 END END AS decimal(18, 2)) AS SalePrice,
    CAST(CASE WHEN Bucket = N'Sales' THEN Amount ELSE 0 END AS decimal(18, 2)) AS Sales,
    CAST(CASE WHEN Bucket = N'Fees' THEN Amount ELSE 0 END AS decimal(18, 2)) AS Fees,
    CAST(CASE WHEN Bucket = N'Refunds' THEN Amount ELSE 0 END AS decimal(18, 2)) AS Refunds,
    CAST(CASE WHEN Bucket = N'Other' THEN Amount ELSE 0 END AS decimal(18, 2)) AS Other
FROM Lines;
