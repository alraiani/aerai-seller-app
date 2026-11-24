/*
  rpt.vw_SettlementLine  (V024: reads the precalculated bucket, category, units, and price per item)
  Purpose : Every settlement line with the column it counts toward and the SKU/price group it belongs to,
            in the currency it was paid in and converted into the marketplace's currency.
  Grain   : One row per core.SettlementLine.
  Sources : core.SettlementLine, rpt.SettlementLineCalc (filled by rpt.usp_RefreshSettlementTotals, which
            owns the bucket and category rules), core.Settlement, rpt.vw_SettlementRate (the exchange
            rate rule).
  Notes   : Same columns as V021. The window functions and rules now run once per promotion instead of on
            every read, so a filter on a settlement, an order, or a marketplace reads only those lines.
            Each Home column is its amount times the rate, rounded to the cent (NULL while no rate is
            known). Single converted lines can differ from a converted total by a cent of rounding.
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
        r.HomeCurrency,
        r.ExchangeRate,
        l.Id AS LineId,
        l.PostedDate,
        l.TransactionType,
        l.AmazonOrderId,
        l.Sku,
        l.AmountType,
        l.AmountDescription,
        l.Amount,
        c.Bucket,
        c.Category,
        c.IsAdCost,
        c.Units,
        c.SalePrice
    FROM core.SettlementLine AS l
    INNER JOIN rpt.SettlementLineCalc AS c ON c.LineId = l.Id
    INNER JOIN core.Settlement AS s ON s.SettlementId = l.SettlementId
    INNER JOIN rpt.vw_SettlementRate AS r ON r.SettlementId = l.SettlementId
)
SELECT
    MarketplaceId,
    SettlementId,
    PeriodStart,
    PeriodEnd,
    Currency,
    HomeCurrency,
    ExchangeRate,
    LineId,
    PostedDate,
    TransactionType,
    AmazonOrderId,
    Sku,
    AmountType,
    AmountDescription,
    Amount,
    Bucket,
    Category,
    Units,
    SalePrice,
    CAST(CASE WHEN Bucket = N'Sales' THEN Amount ELSE 0 END AS decimal(18, 2)) AS Sales,
    CAST(CASE WHEN Bucket = N'Refunds' THEN Amount ELSE 0 END AS decimal(18, 2)) AS Refunds,
    CAST(CASE WHEN Bucket = N'Expenses' THEN Amount ELSE 0 END AS decimal(18, 2)) AS Expenses,
    CAST(CASE WHEN Bucket = N'Reserve' THEN Amount ELSE 0 END AS decimal(18, 2)) AS Reserve,
    CAST(CASE WHEN Category = N'RefundedSales' THEN Amount ELSE 0 END AS decimal(18, 2)) AS RefundedSales,
    CAST(CASE WHEN Category = N'RefundedExpenses' THEN Amount ELSE 0 END AS decimal(18, 2)) AS RefundedExpenses,
    CAST(CASE WHEN Category = N'Shipping' THEN Amount ELSE 0 END AS decimal(18, 2)) AS Shipping,
    CAST(CASE WHEN Category = N'FbaFees' THEN Amount ELSE 0 END AS decimal(18, 2)) AS FbaFees,
    CAST(CASE WHEN Category = N'Commission' THEN Amount ELSE 0 END AS decimal(18, 2)) AS Commission,
    CAST(CASE WHEN Category = N'OtherExpenses' THEN Amount ELSE 0 END AS decimal(18, 2)) AS OtherExpenses,
    CAST(CASE WHEN IsAdCost = 1 THEN Amount ELSE 0 END AS decimal(18, 2)) AS AdCosts,
    CAST(ROUND(SalePrice * ExchangeRate, 2) AS decimal(18, 2)) AS HomeSalePrice,
    CAST(ROUND(Amount * ExchangeRate, 2) AS decimal(18, 2)) AS HomeAmount,
    CAST(ROUND(CASE WHEN Bucket = N'Sales' THEN Amount ELSE 0 END * ExchangeRate, 2) AS decimal(18, 2)) AS HomeSales,
    CAST(ROUND(CASE WHEN Bucket = N'Refunds' THEN Amount ELSE 0 END * ExchangeRate, 2) AS decimal(18, 2)) AS HomeRefunds,
    CAST(ROUND(CASE WHEN Bucket = N'Expenses' THEN Amount ELSE 0 END * ExchangeRate, 2) AS decimal(18, 2)) AS HomeExpenses,
    CAST(ROUND(CASE WHEN Bucket = N'Reserve' THEN Amount ELSE 0 END * ExchangeRate, 2) AS decimal(18, 2)) AS HomeReserve,
    CAST(ROUND(CASE WHEN Category = N'RefundedSales' THEN Amount ELSE 0 END * ExchangeRate, 2) AS decimal(18, 2)) AS HomeRefundedSales,
    CAST(ROUND(CASE WHEN Category = N'RefundedExpenses' THEN Amount ELSE 0 END * ExchangeRate, 2) AS decimal(18, 2)) AS HomeRefundedExpenses,
    CAST(ROUND(CASE WHEN Category = N'Shipping' THEN Amount ELSE 0 END * ExchangeRate, 2) AS decimal(18, 2)) AS HomeShipping,
    CAST(ROUND(CASE WHEN Category = N'FbaFees' THEN Amount ELSE 0 END * ExchangeRate, 2) AS decimal(18, 2)) AS HomeFbaFees,
    CAST(ROUND(CASE WHEN Category = N'Commission' THEN Amount ELSE 0 END * ExchangeRate, 2) AS decimal(18, 2)) AS HomeCommission,
    CAST(ROUND(CASE WHEN Category = N'OtherExpenses' THEN Amount ELSE 0 END * ExchangeRate, 2) AS decimal(18, 2)) AS HomeOtherExpenses,
    CAST(ROUND(CASE WHEN IsAdCost = 1 THEN Amount ELSE 0 END * ExchangeRate, 2) AS decimal(18, 2)) AS HomeAdCosts
FROM Lines;
