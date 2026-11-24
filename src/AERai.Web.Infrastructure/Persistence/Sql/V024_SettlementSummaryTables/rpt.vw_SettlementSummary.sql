/*
  rpt.vw_SettlementSummary  (V024: reads the precalculated totals in rpt.SettlementTotals)
  Purpose : Settlement payouts broken into sales, refunds, expenses, and reserve (the columns of Seller
            Central's statement), with refunds split into refunded sales and refunded expenses and
            expenses into shipping, FBA fees, commission, and other expenses, plus the advertising Amazon
            charged, in the currency they were paid in and converted into the marketplace's currency.
  Grain   : One row per settlement (SettlementId); settlements without lines show zeros.
  Sources : core.Settlement, rpt.SettlementTotals (as-paid totals written by
            rpt.usp_RefreshSettlementTotals, which owns the bucket and category rules),
            rpt.vw_SettlementRate (which owns the exchange rate rule).
  Notes   : Same columns and results as V021; only the as-paid totals are read instead of summed per query.
            Net is the sum of every line and equals the payout. The categories of a bucket add up to it,
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
        ISNULL(t.Units, 0) AS Units,
        ISNULL(t.Sales, 0) AS Sales,
        ISNULL(t.Refunds, 0) AS Refunds,
        ISNULL(t.Expenses, 0) AS Expenses,
        ISNULL(t.Reserve, 0) AS Reserve,
        ISNULL(t.RefundedSales, 0) AS RefundedSales,
        ISNULL(t.RefundedExpenses, 0) AS RefundedExpenses,
        ISNULL(t.Shipping, 0) AS Shipping,
        ISNULL(t.FbaFees, 0) AS FbaFees,
        ISNULL(t.Commission, 0) AS Commission,
        ISNULL(t.OtherExpenses, 0) AS OtherExpenses,
        ISNULL(t.AdCosts, 0) AS AdCosts,
        ISNULL(t.Net, 0) AS Net
    FROM core.Settlement AS s
    INNER JOIN rpt.vw_SettlementRate AS r ON r.SettlementId = s.SettlementId
    LEFT JOIN rpt.SettlementTotals AS t ON t.SettlementId = s.SettlementId
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
