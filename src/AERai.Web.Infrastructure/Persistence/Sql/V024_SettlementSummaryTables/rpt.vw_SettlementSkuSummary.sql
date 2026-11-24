/*
  rpt.vw_SettlementSkuSummary  (V024: reads the precalculated groups in rpt.SettlementSkuTotals)
  Purpose : A settlement broken down by what was sold, for the settlement summary page.
  Grain   : One row per settlement, SKU, price per item, and currency (SKU and price are NULL for
            lines that belong to no SKU, such as service fees).
  Sources : rpt.SettlementSkuTotals (as-paid group totals written by rpt.usp_RefreshSettlementTotals),
            core.Settlement, rpt.vw_SettlementRate, core.Product (ASIN and title by SKU; NULL when the SKU
            is not in the catalog).
  Notes   : Same columns and results as V021. The columns are the same buckets and categories as
            rpt.vw_SettlementSummary; the rows of one settlement add up to its row there. Net is the payout
            share of the group. Units is how many items sold at this price (refunds do not reduce it).
            The Home columns convert each group's totals at the settlement's rate (NULL while no rate is
            known), so the groups of one settlement can differ from its converted net by a cent or two.
            HomeOtherExpenses and HomeRefundedExpenses take what is left of the row's converted expenses
            and refunds, so a row's categories always add up to its buckets.
*/
CREATE OR ALTER VIEW rpt.vw_SettlementSkuSummary
AS
WITH Groups AS (
    SELECT
        s.MarketplaceId,
        s.SettlementId,
        s.PeriodStart,
        s.PeriodEnd,
        s.Currency,
        r.HomeCurrency,
        r.ExchangeRate,
        g.Sku,
        p.Asin,
        p.Title,
        g.SalePrice,
        CAST(ROUND(g.SalePrice * r.ExchangeRate, 2) AS decimal(18, 2)) AS HomeSalePrice,
        g.Units,
        g.OrderCount,
        g.LineCount,
        g.Sales,
        g.Refunds,
        g.Expenses,
        g.Reserve,
        g.RefundedSales,
        g.RefundedExpenses,
        g.Shipping,
        g.FbaFees,
        g.Commission,
        g.OtherExpenses,
        g.Net,
        CAST(ROUND(g.Sales * r.ExchangeRate, 2) AS decimal(18, 2)) AS HomeSales,
        CAST(ROUND(g.Refunds * r.ExchangeRate, 2) AS decimal(18, 2)) AS HomeRefunds,
        CAST(ROUND(g.Expenses * r.ExchangeRate, 2) AS decimal(18, 2)) AS HomeExpenses,
        CAST(ROUND(g.Reserve * r.ExchangeRate, 2) AS decimal(18, 2)) AS HomeReserve,
        CAST(ROUND(g.RefundedSales * r.ExchangeRate, 2) AS decimal(18, 2)) AS HomeRefundedSales,
        CAST(ROUND(g.Shipping * r.ExchangeRate, 2) AS decimal(18, 2)) AS HomeShipping,
        CAST(ROUND(g.FbaFees * r.ExchangeRate, 2) AS decimal(18, 2)) AS HomeFbaFees,
        CAST(ROUND(g.Commission * r.ExchangeRate, 2) AS decimal(18, 2)) AS HomeCommission,
        CAST(ROUND(g.Net * r.ExchangeRate, 2) AS decimal(18, 2)) AS HomeNet
    FROM rpt.SettlementSkuTotals AS g
    INNER JOIN core.Settlement AS s ON s.SettlementId = g.SettlementId
    INNER JOIN rpt.vw_SettlementRate AS r ON r.SettlementId = g.SettlementId
    LEFT JOIN core.Product AS p ON p.Sku = g.Sku
)
SELECT
    *,
    CAST(HomeRefunds - HomeRefundedSales AS decimal(18, 2)) AS HomeRefundedExpenses,
    CAST(HomeExpenses - HomeShipping - HomeFbaFees - HomeCommission AS decimal(18, 2)) AS HomeOtherExpenses
FROM Groups;
