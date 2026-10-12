/*
  rpt.vw_SettlementLine  (V013)
  Purpose : Every settlement line with the bucket it counts toward and the SKU/sale price group it belongs
            to. The single definition of the Sales/Fees/Refunds/Other rule, so the settlement list, the
            per-SKU summary, and the line-level drill-down always add up to the same payout.
  Grain   : One row per core.SettlementLine.
  Sources : core.Settlement, core.SettlementLine.
  Notes   : Buckets, in priority order:
              Refunds - transaction-type 'Refund' (all of its amounts, including refunded fees)
              Sales   - transaction-type 'Order' with amount-type 'ItemPrice'
              Fees    - amount-type 'ItemFees', or transaction-type 'ServiceFee'
              Other   - everything else (promotions, reimbursements, adjustments)
            SalePrice is the absolute Principal item price of the line's (settlement, order, SKU,
            transaction type): settlement files carry no per-unit price, so this is what the order line
            sold (or was refunded) for. Orders and refunds are kept apart so a refund does not cancel
            the sale it reverses. It is NULL for lines with no SKU or no Principal (e.g. service fees).
*/
CREATE OR ALTER VIEW rpt.vw_SettlementLine
AS
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
    CAST(CASE WHEN l.Sku IS NULL THEN NULL ELSE ABS(SUM(CASE WHEN l.AmountType = N'ItemPrice' AND l.AmountDescription = N'Principal' THEN l.Amount END)
        OVER (PARTITION BY l.SettlementId, ISNULL(l.AmazonOrderId, N''), l.Sku, l.TransactionType)) END AS decimal(18, 2)) AS SalePrice,
    CAST(CASE WHEN b.Bucket = N'Sales' THEN l.Amount ELSE 0 END AS decimal(18, 2)) AS Sales,
    CAST(CASE WHEN b.Bucket = N'Fees' THEN l.Amount ELSE 0 END AS decimal(18, 2)) AS Fees,
    CAST(CASE WHEN b.Bucket = N'Refunds' THEN l.Amount ELSE 0 END AS decimal(18, 2)) AS Refunds,
    CAST(CASE WHEN b.Bucket = N'Other' THEN l.Amount ELSE 0 END AS decimal(18, 2)) AS Other
FROM core.Settlement AS s
INNER JOIN core.SettlementLine AS l ON l.SettlementId = s.SettlementId
CROSS APPLY (
    SELECT CASE
        WHEN l.TransactionType = N'Refund' THEN N'Refunds'
        WHEN l.TransactionType = N'Order' AND l.AmountType = N'ItemPrice' THEN N'Sales'
        WHEN l.AmountType = N'ItemFees' OR l.TransactionType = N'ServiceFee' THEN N'Fees'
        ELSE N'Other'
    END AS Bucket
) AS b;
