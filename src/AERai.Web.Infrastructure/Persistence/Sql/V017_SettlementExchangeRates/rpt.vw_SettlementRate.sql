/*
  rpt.vw_SettlementRate  (V017)
  Purpose : The exchange rate that converts each settlement into its marketplace's currency. The single
            definition of which rate applies, used by every settlement view and the bookkeeping export.
  Grain   : One row per core.Settlement.
  Sources : core.Settlement, core.Marketplace, core.SettlementExchangeRate.
  Notes   : ExchangeRate is 1 when the settlement was paid in its marketplace's currency (or has no
            currency), else the Operator's rate when set, else the daily feed's rate, else NULL (no rate
            known yet: the views then leave every Home amount NULL instead of guessing).
            RateSource is Same / Manual / Market / NULL accordingly. A rate row converts into the
            currency it was fetched for (ToCurrency); if that no longer is the marketplace's currency
            it does not apply.
            ManualHomeAmount is the converted payout an Operator entered (what reached the bank); when
            set it is the settlement's HomeNet exactly.
*/
CREATE OR ALTER VIEW rpt.vw_SettlementRate
AS
SELECT
    s.SettlementId,
    s.MarketplaceId,
    s.Currency,
    m.Currency AS HomeCurrency,
    CAST(CASE
        WHEN s.Currency IS NULL OR s.Currency = m.Currency THEN 1
        ELSE COALESCE(x.ManualRate, x.MarketRate)
    END AS decimal(19, 10)) AS ExchangeRate,
    CAST(CASE
        WHEN s.Currency IS NULL OR s.Currency = m.Currency THEN N'Same'
        WHEN x.ManualRate IS NOT NULL THEN N'Manual'
        WHEN x.MarketRate IS NOT NULL THEN N'Market'
        ELSE NULL
    END AS nvarchar(8)) AS RateSource,
    CASE WHEN s.Currency IS NULL OR s.Currency = m.Currency THEN NULL ELSE x.ManualHomeAmount END AS ManualHomeAmount
FROM core.Settlement AS s
INNER JOIN core.Marketplace AS m ON m.MarketplaceId = s.MarketplaceId
LEFT JOIN core.SettlementExchangeRate AS x ON x.SettlementId = s.SettlementId AND x.ToCurrency = m.Currency;
