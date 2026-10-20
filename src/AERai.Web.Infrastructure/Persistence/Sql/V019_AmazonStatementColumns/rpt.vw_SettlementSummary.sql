/*
  rpt.vw_SettlementSummary  (V019: Amazon's statement columns)
  Purpose : Settlement payouts broken into sales, refunds, expenses, and reserve (the columns of Seller
            Central's statement), for the Settlements page, in the currency they were paid in and
            converted into the marketplace's currency.
  Grain   : One row per settlement (SettlementId); settlements without lines show zeros.
  Sources : core.Settlement, rpt.vw_SettlementLine (which owns the bucketing rule), rpt.vw_SettlementRate
            (which owns the exchange rate rule).
  Notes   : Net is the sum of every line and equals the payout.
            The Home columns convert the totals (not the rounded lines) at the settlement's rate and are
            NULL while no rate is known. HomeNet is the amount an Operator entered when they gave one, so
            it matches the bank exactly; HomeExpenses takes whatever is left of HomeNet after converted
            sales, refunds, and reserve, so the four always add up to it.
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
        CAST(CASE WHEN ExchangeRate IS NULL THEN NULL
                  ELSE COALESCE(ManualHomeAmount, ROUND(Net * ExchangeRate, 2)) END AS decimal(18, 2)) AS HomeNet
    FROM Totals
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
    Net,
    HomeSales,
    HomeRefunds,
    CAST(HomeNet - HomeSales - HomeRefunds - HomeReserve AS decimal(18, 2)) AS HomeExpenses,
    HomeReserve,
    HomeNet
FROM Converted;
