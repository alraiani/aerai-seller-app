/*
  rpt.vw_SettlementSummary  (V013: built on rpt.vw_SettlementLine)
  Purpose : Settlement payouts broken into sales, fees, refunds, and other, for the Settlements page.
  Grain   : One row per settlement (SettlementId); settlements without lines show zeros.
  Sources : core.Settlement, rpt.vw_SettlementLine (which owns the bucketing rule).
  Notes   : Net is the sum of every line and equals the payout.
*/
CREATE OR ALTER VIEW rpt.vw_SettlementSummary
AS
SELECT
    s.MarketplaceId,
    s.SettlementId,
    s.PeriodStart,
    s.PeriodEnd,
    s.Currency,
    CAST(ISNULL(SUM(l.Sales), 0) AS decimal(18, 2)) AS Sales,
    CAST(ISNULL(SUM(l.Fees), 0) AS decimal(18, 2)) AS Fees,
    CAST(ISNULL(SUM(l.Refunds), 0) AS decimal(18, 2)) AS Refunds,
    CAST(ISNULL(SUM(l.Other), 0) AS decimal(18, 2)) AS Other,
    CAST(ISNULL(SUM(l.Amount), 0) AS decimal(18, 2)) AS Net
FROM core.Settlement AS s
LEFT JOIN rpt.vw_SettlementLine AS l ON l.SettlementId = s.SettlementId
GROUP BY s.MarketplaceId, s.SettlementId, s.PeriodStart, s.PeriodEnd, s.Currency;
