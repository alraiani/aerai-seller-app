/*
  rpt.vw_SettlementSummary  (V005: adds MarketplaceId)
  Purpose : Settlement payouts broken into sales, fees, refunds, and other, for the Settlements page.
  Grain   : One row per settlement (SettlementId).
  Sources : core.Settlement, core.SettlementLine.
  Notes   : Bucketing rules, in priority order:
              Refunds - transaction-type 'Refund' (all of its amounts, including refunded fees)
              Sales   - transaction-type 'Order' with amount-type 'ItemPrice'
              Fees    - amount-type 'ItemFees', or transaction-type 'ServiceFee'
              Other   - everything else (promotions, reimbursements, adjustments)
            Net is the sum of every line and equals the payout.
*/
CREATE OR ALTER VIEW rpt.vw_SettlementSummary
AS
SELECT
    s.MarketplaceId,
    s.SettlementId,
    s.PeriodStart,
    s.PeriodEnd,
    s.Currency,
    CAST(ISNULL(SUM(CASE WHEN b.Bucket = N'Sales' THEN l.Amount END), 0) AS decimal(18, 2)) AS Sales,
    CAST(ISNULL(SUM(CASE WHEN b.Bucket = N'Fees' THEN l.Amount END), 0) AS decimal(18, 2)) AS Fees,
    CAST(ISNULL(SUM(CASE WHEN b.Bucket = N'Refunds' THEN l.Amount END), 0) AS decimal(18, 2)) AS Refunds,
    CAST(ISNULL(SUM(CASE WHEN b.Bucket = N'Other' THEN l.Amount END), 0) AS decimal(18, 2)) AS Other,
    CAST(ISNULL(SUM(l.Amount), 0) AS decimal(18, 2)) AS Net
FROM core.Settlement AS s
LEFT JOIN core.SettlementLine AS l ON l.SettlementId = s.SettlementId
CROSS APPLY (
    SELECT CASE
        WHEN l.TransactionType = N'Refund' THEN N'Refunds'
        WHEN l.TransactionType = N'Order' AND l.AmountType = N'ItemPrice' THEN N'Sales'
        WHEN l.AmountType = N'ItemFees' OR l.TransactionType = N'ServiceFee' THEN N'Fees'
        ELSE N'Other'
    END AS Bucket
) AS b
GROUP BY s.MarketplaceId, s.SettlementId, s.PeriodStart, s.PeriodEnd, s.Currency;
