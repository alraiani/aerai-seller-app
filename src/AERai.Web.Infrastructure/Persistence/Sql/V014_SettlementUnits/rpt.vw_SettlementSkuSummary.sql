/*
  rpt.vw_SettlementSkuSummary  (V014: adds Units)
  Purpose : A settlement broken down by what was sold, for the settlement summary page.
  Grain   : One row per settlement, SKU, price per item, and currency (SKU and price are NULL for
            lines that belong to no SKU, such as service fees).
  Sources : rpt.vw_SettlementLine, core.Product (ASIN and title by SKU; NULL when the SKU is not in the catalog).
  Notes   : The columns are the same buckets as rpt.vw_SettlementSummary; the rows of one settlement add up
            to its row there. Net is the payout share of the group. Units is how many items sold at this
            price (refunds do not reduce it; refund lines carry no quantity).
*/
CREATE OR ALTER VIEW rpt.vw_SettlementSkuSummary
AS
SELECT
    l.MarketplaceId,
    l.SettlementId,
    l.PeriodStart,
    l.PeriodEnd,
    l.Currency,
    l.Sku,
    p.Asin,
    p.Title,
    l.SalePrice,
    CAST(SUM(l.Units) AS int) AS Units,
    COUNT(DISTINCT l.AmazonOrderId) AS OrderCount,
    COUNT(*) AS LineCount,
    CAST(SUM(l.Sales) AS decimal(18, 2)) AS Sales,
    CAST(SUM(l.Fees) AS decimal(18, 2)) AS Fees,
    CAST(SUM(l.Refunds) AS decimal(18, 2)) AS Refunds,
    CAST(SUM(l.Other) AS decimal(18, 2)) AS Other,
    CAST(SUM(l.Amount) AS decimal(18, 2)) AS Net
FROM rpt.vw_SettlementLine AS l
LEFT JOIN core.Product AS p ON p.Sku = l.Sku
GROUP BY l.MarketplaceId, l.SettlementId, l.PeriodStart, l.PeriodEnd, l.Currency, l.Sku, p.Asin, p.Title, l.SalePrice;
