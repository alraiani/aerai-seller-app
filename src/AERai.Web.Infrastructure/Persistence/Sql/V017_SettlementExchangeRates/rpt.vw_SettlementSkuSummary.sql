/*
  rpt.vw_SettlementSkuSummary  (V017: amounts converted into the marketplace currency)
  Purpose : A settlement broken down by what was sold, for the settlement summary page.
  Grain   : One row per settlement, SKU, price per item, and currency (SKU and price are NULL for
            lines that belong to no SKU, such as service fees).
  Sources : rpt.vw_SettlementLine, core.Product (ASIN and title by SKU; NULL when the SKU is not in the catalog).
  Notes   : The columns are the same buckets as rpt.vw_SettlementSummary; the rows of one settlement add up
            to its row there. Net is the payout share of the group. Units is how many items sold at this
            price (refunds do not reduce it; refund lines carry no quantity).
            The Home columns convert each group's totals at the settlement's rate (NULL while no rate is
            known), so the groups of one settlement can differ from its converted net by a cent or two.
*/
CREATE OR ALTER VIEW rpt.vw_SettlementSkuSummary
AS
SELECT
    l.MarketplaceId,
    l.SettlementId,
    l.PeriodStart,
    l.PeriodEnd,
    l.Currency,
    l.HomeCurrency,
    l.ExchangeRate,
    l.Sku,
    p.Asin,
    p.Title,
    l.SalePrice,
    l.HomeSalePrice,
    CAST(SUM(l.Units) AS int) AS Units,
    COUNT(DISTINCT l.AmazonOrderId) AS OrderCount,
    COUNT(*) AS LineCount,
    CAST(SUM(l.Sales) AS decimal(18, 2)) AS Sales,
    CAST(SUM(l.Fees) AS decimal(18, 2)) AS Fees,
    CAST(SUM(l.Refunds) AS decimal(18, 2)) AS Refunds,
    CAST(SUM(l.Other) AS decimal(18, 2)) AS Other,
    CAST(SUM(l.Amount) AS decimal(18, 2)) AS Net,
    CAST(ROUND(SUM(l.Sales) * l.ExchangeRate, 2) AS decimal(18, 2)) AS HomeSales,
    CAST(ROUND(SUM(l.Fees) * l.ExchangeRate, 2) AS decimal(18, 2)) AS HomeFees,
    CAST(ROUND(SUM(l.Refunds) * l.ExchangeRate, 2) AS decimal(18, 2)) AS HomeRefunds,
    CAST(ROUND(SUM(l.Other) * l.ExchangeRate, 2) AS decimal(18, 2)) AS HomeOther,
    CAST(ROUND(SUM(l.Amount) * l.ExchangeRate, 2) AS decimal(18, 2)) AS HomeNet
FROM rpt.vw_SettlementLine AS l
LEFT JOIN core.Product AS p ON p.Sku = l.Sku
GROUP BY l.MarketplaceId, l.SettlementId, l.PeriodStart, l.PeriodEnd, l.Currency, l.HomeCurrency, l.ExchangeRate,
         l.Sku, p.Asin, p.Title, l.SalePrice, l.HomeSalePrice;
