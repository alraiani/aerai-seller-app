/*
  rpt.vw_SettlementSkuSummary  (V021: expenses and refunds broken out)
  Purpose : A settlement broken down by what was sold, for the settlement summary page.
  Grain   : One row per settlement, SKU, price per item, and currency (SKU and price are NULL for
            lines that belong to no SKU, such as service fees).
  Sources : rpt.vw_SettlementLine, core.Product (ASIN and title by SKU; NULL when the SKU is not in the catalog).
  Notes   : The columns are the same buckets and categories as rpt.vw_SettlementSummary; the rows of one
            settlement add up to its row there. Net is the payout share of the group. Units is how many
            items sold at this price (refunds do not reduce it; refund lines carry no quantity).
            The Home columns convert each group's totals at the settlement's rate (NULL while no rate is
            known), so the groups of one settlement can differ from its converted net by a cent or two.
            HomeOtherExpenses and HomeRefundedExpenses take what is left of the row's converted expenses
            and refunds, so a row's categories always add up to its buckets.
*/
CREATE OR ALTER VIEW rpt.vw_SettlementSkuSummary
AS
WITH Groups AS (
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
        CAST(SUM(l.Refunds) AS decimal(18, 2)) AS Refunds,
        CAST(SUM(l.Expenses) AS decimal(18, 2)) AS Expenses,
        CAST(SUM(l.Reserve) AS decimal(18, 2)) AS Reserve,
        CAST(SUM(l.RefundedSales) AS decimal(18, 2)) AS RefundedSales,
        CAST(SUM(l.RefundedExpenses) AS decimal(18, 2)) AS RefundedExpenses,
        CAST(SUM(l.Shipping) AS decimal(18, 2)) AS Shipping,
        CAST(SUM(l.FbaFees) AS decimal(18, 2)) AS FbaFees,
        CAST(SUM(l.Commission) AS decimal(18, 2)) AS Commission,
        CAST(SUM(l.OtherExpenses) AS decimal(18, 2)) AS OtherExpenses,
        CAST(SUM(l.Amount) AS decimal(18, 2)) AS Net,
        CAST(ROUND(SUM(l.Sales) * l.ExchangeRate, 2) AS decimal(18, 2)) AS HomeSales,
        CAST(ROUND(SUM(l.Refunds) * l.ExchangeRate, 2) AS decimal(18, 2)) AS HomeRefunds,
        CAST(ROUND(SUM(l.Expenses) * l.ExchangeRate, 2) AS decimal(18, 2)) AS HomeExpenses,
        CAST(ROUND(SUM(l.Reserve) * l.ExchangeRate, 2) AS decimal(18, 2)) AS HomeReserve,
        CAST(ROUND(SUM(l.RefundedSales) * l.ExchangeRate, 2) AS decimal(18, 2)) AS HomeRefundedSales,
        CAST(ROUND(SUM(l.Shipping) * l.ExchangeRate, 2) AS decimal(18, 2)) AS HomeShipping,
        CAST(ROUND(SUM(l.FbaFees) * l.ExchangeRate, 2) AS decimal(18, 2)) AS HomeFbaFees,
        CAST(ROUND(SUM(l.Commission) * l.ExchangeRate, 2) AS decimal(18, 2)) AS HomeCommission,
        CAST(ROUND(SUM(l.Amount) * l.ExchangeRate, 2) AS decimal(18, 2)) AS HomeNet
    FROM rpt.vw_SettlementLine AS l
    LEFT JOIN core.Product AS p ON p.Sku = l.Sku
    GROUP BY l.MarketplaceId, l.SettlementId, l.PeriodStart, l.PeriodEnd, l.Currency, l.HomeCurrency, l.ExchangeRate,
             l.Sku, p.Asin, p.Title, l.SalePrice, l.HomeSalePrice
)
SELECT
    *,
    CAST(HomeRefunds - HomeRefundedSales AS decimal(18, 2)) AS HomeRefundedExpenses,
    CAST(HomeExpenses - HomeShipping - HomeFbaFees - HomeCommission AS decimal(18, 2)) AS HomeOtherExpenses
FROM Groups;
