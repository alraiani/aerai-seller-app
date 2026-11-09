/*
  rpt.vw_ProfitLine  (V023)
  Purpose : Every amount that makes up profit & loss, dated by the day Amazon posted it (accrual, not payout),
            with the P&L category it counts toward. The single definition of those categories and of where
            the ledger takes over from settlement lines, so the profit page, its months, its SKUs, and its
            reconciliation always add up the same way.
  Grain   : One row per core.FinancialTransactionLine (ledger) or core.SettlementLine (history before it).
  Sources : core.FinancialTransaction(+Line) — Amazon's transaction ledger (Finances API), posted within about a
            day, held or paid out; rpt.vw_SettlementLine — settlement lines, for days before the marketplace's
            ledger begins; core.Marketplace.
  Cutover : Per marketplace, the ledger counts from the UTC day after its earliest posted transaction (its first
            day may be partial); settlement lines count before that day. A marketplace without a ledger is all
            settlement lines. No day is ever counted from both.
  Currency: Each marketplace in its own currency. Settlement lines belong to the marketplace whose settlements
            list holds them (SettlementMarketplaceId, else its own) and whose currency they are paid in, so the
            US list's CAD lines are Canada's and its MXN lines (no Mexico marketplace) are left out.
  Category: Sales, RefundedSales, RefundedExpenses, Shipping, Commission, FbaFees, OtherExpenses (as on the
            settlement pages), plus Advertising (ad charges Amazon billed; profit uses Ads API spend for days it
            has synced), Reserve (account-level reserve: not profit), and Transfer (payouts to the bank: not
            profit). Ledger rules, first match wins:
              Transfer      - transaction type or description naming a transfer/disbursement/payout
              Reserve       - description or breakdown naming a reserve
              Advertising   - transaction type ProductAdsPayment, or 'advertis…' in the description
              Refunds       - transaction type 'Refund…', 'GuaranteeClaim…', 'Chargeback…':
                                RefundedSales    product charges and taxes given back
                                RefundedExpenses everything else (fees given back, refund commission)
              Sales         - product charges and taxes (collected and withheld cancel), and positive
                              reimbursements
              Shipping      - shipping chargebacks and inbound/partnered-carrier/AWD transportation
              Commission    - breakdown 'Commission' (referral fees)
              FbaFees       - FBA, storage, removal, and disposal fees
              OtherExpenses - everything else (promotions, closing fees, subscriptions, unitemized)
            Unmapped ledger breakdowns fall into OtherExpenses and are listed on the reconciliation view.
  Columns : IsPrincipal marks an item's principal (its price before tax), used to count refunded units.
            Units is the sold quantity, on one line per item only. Held is set while Amazon holds the money.
*/
CREATE OR ALTER VIEW rpt.vw_ProfitLine
AS
WITH LedgerStart AS (
    SELECT t.MarketplaceId,
           CAST(DATEADD(day, 1, CAST(SWITCHOFFSET(MIN(t.PostedDate), '+00:00') AS date)) AS datetimeoffset) AS StartsAt
    FROM core.FinancialTransaction AS t
    GROUP BY t.MarketplaceId
),
Ledger AS (
    SELECT t.MarketplaceId,
           CAST(N'Ledger' AS nvarchar(10)) AS Source,
           t.PostedDate,
           t.AmazonOrderId,
           t.SettlementId,
           t.TransactionType,
           l.BreakdownPath AS Detail,
           l.Sku,
           l.Units,
           l.Amount,
           CAST(CASE WHEN t.Status = N'DEFERRED' THEN 1 ELSE 0 END AS bit) AS Held,
           CAST(CASE WHEN l.BreakdownPath LIKE N'%Principal' THEN 1 ELSE 0 END AS bit) AS IsPrincipal,
           CAST(CASE
                WHEN t.TransactionType LIKE N'%Transfer%' OR t.TransactionType LIKE N'%Disbursement%'
                     OR t.Description LIKE N'%Transfer%' OR t.Description LIKE N'%Disbursement%' OR t.Description LIKE N'%Payout%'
                    THEN N'Transfer'
                WHEN t.Description LIKE N'%Reserve%' OR l.BreakdownPath LIKE N'%Reserve%'
                    THEN N'Reserve'
                WHEN t.TransactionType LIKE N'ProductAds%' OR t.Description LIKE N'%Advertis%'
                    THEN N'Advertising'
                WHEN t.TransactionType LIKE N'Refund%' OR t.TransactionType LIKE N'GuaranteeClaim%' OR t.TransactionType LIKE N'Chargeback%'
                    THEN CASE WHEN l.BreakdownPath LIKE N'ProductCharges%' OR l.BreakdownPath LIKE N'%Tax%'
                              THEN N'RefundedSales' ELSE N'RefundedExpenses' END
                WHEN l.BreakdownPath LIKE N'ProductCharges%' OR l.BreakdownPath LIKE N'%Tax%'
                     OR (t.TransactionType LIKE N'%Reimbursement%' AND l.Amount > 0)
                    THEN N'Sales'
                WHEN l.BreakdownPath LIKE N'%ShippingChargeback%' OR l.BreakdownPath LIKE N'%ShippingHB%'
                     OR l.BreakdownPath LIKE N'%InboundTransportation%' OR l.BreakdownPath LIKE N'%PartneredCarrier%'
                     OR l.BreakdownPath LIKE N'%AWDTransportation%' OR t.Description LIKE N'%Inbound Transportation%'
                    THEN N'Shipping'
                WHEN l.BreakdownPath LIKE N'%Commission%'
                    THEN N'Commission'
                WHEN l.BreakdownPath LIKE N'%FBA%' OR t.TransactionType LIKE N'%FBA%' OR l.BreakdownPath LIKE N'%Storage%'
                     OR t.Description LIKE N'%Storage%' OR l.BreakdownPath LIKE N'%Removal%' OR l.BreakdownPath LIKE N'%Disposal%'
                    THEN N'FbaFees'
                ELSE N'OtherExpenses'
           END AS nvarchar(20)) AS Category
    FROM core.FinancialTransaction AS t
    JOIN core.FinancialTransactionLine AS l ON l.FinancialTransactionId = t.Id
    JOIN LedgerStart AS s ON s.MarketplaceId = t.MarketplaceId
    WHERE t.PostedDate >= s.StartsAt
),
History AS (
    SELECT m.MarketplaceId,
           CAST(N'Settlement' AS nvarchar(10)) AS Source,
           l.PostedDate,
           l.AmazonOrderId,
           l.SettlementId,
           l.TransactionType,
           CAST(CONCAT(l.AmountType, N'/', l.AmountDescription) AS nvarchar(400)) AS Detail,
           l.Sku,
           l.Units,
           l.Amount,
           CAST(0 AS bit) AS Held,
           CAST(CASE WHEN l.AmountType = N'ItemPrice' AND l.AmountDescription = N'Principal' THEN 1 ELSE 0 END AS bit) AS IsPrincipal,
           CAST(CASE WHEN l.AdCosts <> 0 THEN N'Advertising' ELSE l.Category END AS nvarchar(20)) AS Category
    FROM rpt.vw_SettlementLine AS l
    JOIN core.Marketplace AS m
      ON COALESCE(m.SettlementMarketplaceId, m.MarketplaceId) = l.MarketplaceId
     AND m.Currency = l.Currency
    LEFT JOIN LedgerStart AS s ON s.MarketplaceId = m.MarketplaceId
    WHERE l.PostedDate IS NOT NULL
      AND (s.StartsAt IS NULL OR l.PostedDate < s.StartsAt)
)
SELECT MarketplaceId, Source, PostedDate, AmazonOrderId, SettlementId, TransactionType, Detail, Sku, Units, Amount, Held, IsPrincipal, Category
FROM Ledger
UNION ALL
SELECT MarketplaceId, Source, PostedDate, AmazonOrderId, SettlementId, TransactionType, Detail, Sku, Units, Amount, Held, IsPrincipal, Category
FROM History;
