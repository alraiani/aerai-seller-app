/*
  rpt.vw_SettlementSummary  (V021: expenses and refunds broken out)
  Purpose : Settlement payouts broken into sales, refunds, expenses, and reserve (the columns of Seller
            Central's statement), with refunds split into refunded sales and refunded expenses and
            expenses into shipping, FBA fees, commission, and other expenses, plus the advertising Amazon
            charged, for the Settlements pages, in the currency they were paid in and converted into the
            marketplace's currency.
  Grain   : One row per settlement (SettlementId); settlements without lines show zeros.
  Sources : core.Settlement, rpt.vw_SettlementLine (which owns the bucket and category rules),
            rpt.vw_SettlementRate (which owns the exchange rate rule).
  Notes   : Net is the sum of every line and equals the payout. The categories of a bucket add up to it,
            and AdCosts is part of OtherExpenses (not an amount of its own).
            The Home columns convert the totals (not the rounded lines) at the settlement's rate and are
            NULL while no rate is known. HomeNet is the amount an Operator entered when they gave one, so
            it matches the bank exactly; HomeExpenses takes whatever is left of HomeNet after converted
            sales, refunds, and reserve, so the four always add up to it. In the same way
            HomeOtherExpenses is what is left of HomeExpenses after shipping, FBA fees, and commission,
            and HomeRefundedExpenses what is left of HomeRefunds after refunded sales.
*/
CREATE OR ALTER VIEW rpt.vw_SettlementSummary
AS
WITH Totals AS (
    SELECT
        s.MarketplaceId,
        s.SettlementId,
        s.PeriodStart,
        s.PeriodEnd,
        s.DepositDate,
        s.Currency,
        r.HomeCurrency,
        r.ExchangeRate,
        r.RateSource,
        r.ManualHomeAmount,
        CAST(ISNULL(SUM(l.Units), 0) AS int) AS Units,
        CAST(ISNULL(SUM(l.Sales), 0) AS decimal(18, 2)) AS Sales,
        CAST(ISNULL(SUM(l.Refunds), 0) AS decimal(18, 2)) AS Refunds,
        CAST(ISNULL(SUM(l.Expenses), 0) AS decimal(18, 2)) AS Expenses,
        CAST(ISNULL(SUM(l.Reserve), 0) AS decimal(18, 2)) AS Reserve,
        CAST(ISNULL(SUM(l.RefundedSales), 0) AS decimal(18, 2)) AS RefundedSales,
        CAST(ISNULL(SUM(l.RefundedExpenses), 0) AS decimal(18, 2)) AS RefundedExpenses,
        CAST(ISNULL(SUM(l.Shipping), 0) AS decimal(18, 2)) AS Shipping,
        CAST(ISNULL(SUM(l.FbaFees), 0) AS decimal(18, 2)) AS FbaFees,
        CAST(ISNULL(SUM(l.Commission), 0) AS decimal(18, 2)) AS Commission,
        CAST(ISNULL(SUM(l.OtherExpenses), 0) AS decimal(18, 2)) AS OtherExpenses,
        CAST(ISNULL(SUM(l.AdCosts), 0) AS decimal(18, 2)) AS AdCosts,
        CAST(ISNULL(SUM(l.Amount), 0) AS decimal(18, 2)) AS Net
    FROM core.Settlement AS s
    INNER JOIN rpt.vw_SettlementRate AS r ON r.SettlementId = s.SettlementId
    LEFT JOIN rpt.vw_SettlementLine AS l ON l.SettlementId = s.SettlementId
    GROUP BY s.MarketplaceId, s.SettlementId, s.PeriodStart, s.PeriodEnd, s.DepositDate, s.Currency,
             r.HomeCurrency, r.ExchangeRate, r.RateSource, r.ManualHomeAmount
),
Converted AS (
    SELECT
        *,
        CAST(ROUND(Sales * ExchangeRate, 2) AS decimal(18, 2)) AS HomeSales,
        CAST(ROUND(Refunds * ExchangeRate, 2) AS decimal(18, 2)) AS HomeRefunds,
        CAST(ROUND(Reserve * ExchangeRate, 2) AS decimal(18, 2)) AS HomeReserve,
        CAST(ROUND(RefundedSales * ExchangeRate, 2) AS decimal(18, 2)) AS HomeRefundedSales,
        CAST(ROUND(Shipping * ExchangeRate, 2) AS decimal(18, 2)) AS HomeShipping,
        CAST(ROUND(FbaFees * ExchangeRate, 2) AS decimal(18, 2)) AS HomeFbaFees,
        CAST(ROUND(Commission * ExchangeRate, 2) AS decimal(18, 2)) AS HomeCommission,
        CAST(ROUND(AdCosts * ExchangeRate, 2) AS decimal(18, 2)) AS HomeAdCosts,
        CAST(CASE WHEN ExchangeRate IS NULL THEN NULL
                  ELSE COALESCE(ManualHomeAmount, ROUND(Net * ExchangeRate, 2)) END AS decimal(18, 2)) AS HomeNet
    FROM Totals
),
Remainders AS (
    SELECT
        *,
        CAST(HomeNet - HomeSales - HomeRefunds - HomeReserve AS decimal(18, 2)) AS HomeExpenses
    FROM Converted
)
SELECT
    MarketplaceId,
    SettlementId,
    PeriodStart,
    PeriodEnd,
    DepositDate,
    Currency,
    HomeCurrency,
    ExchangeRate,
    RateSource,
    Units,
    Sales,
    Refunds,
    Expenses,
    Reserve,
    RefundedSales,
    RefundedExpenses,
    Shipping,
    FbaFees,
    Commission,
    OtherExpenses,
    AdCosts,
    Net,
    HomeSales,
    HomeRefunds,
    HomeExpenses,
    HomeReserve,
    HomeRefundedSales,
    CAST(HomeRefunds - HomeRefundedSales AS decimal(18, 2)) AS HomeRefundedExpenses,
    HomeShipping,
    HomeFbaFees,
    HomeCommission,
    CAST(HomeExpenses - HomeShipping - HomeFbaFees - HomeCommission AS decimal(18, 2)) AS HomeOtherExpenses,
    HomeAdCosts,
    HomeNet
FROM Remainders;
