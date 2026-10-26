/*
  rpt.vw_SettlementLine  (V021: expenses and refunds broken out)
  Purpose : Every settlement line with the column it counts toward and the SKU/price group it belongs
            to. The single definition of the Sales/Refunds/Expenses/Reserve rule and of the finer
            categories inside it, so the settlement list, the per-SKU summary, and the line-level
            drill-down always add up to the same payout, and each column matches the one Seller Central
            shows on the settlement's statement.
  Grain   : One row per core.SettlementLine.
  Sources : core.Settlement, core.SettlementLine, rpt.vw_SettlementRate (the exchange rate rule).
  Notes   : Buckets, in priority order (checked against Seller Central's statements to the cent):
              Reserve  - amount-description 'Previous Reserve Amount Balance' or 'Current Reserve Amount'
                         (Amazon's account level reserve: held back from one payout, released in a later one)
              Refunds  - transaction-type 'Refund…' (all of its amounts, including refunded fees and taxes)
              Sales    - transaction-type 'Order…' with amount-type 'ItemPrice' or 'ItemWithheldTax' (the
                         tax Amazon collects and the facilitator tax it withholds cancel out), and FBA
                         inventory reimbursements Amazon pays (positive amounts)
              Expenses - everything else: item fees, promotions, FBA and service fees, advertising,
                         subscriptions, reimbursement clawbacks, deductions
            Category splits two buckets further (the others keep the bucket's name); the categories of a
            bucket always add up to it:
              Refunds  -> RefundedSales    (amount-type 'ItemPrice' or 'ItemWithheldTax': the price,
                                            shipping, and tax given back)
                          RefundedExpenses (everything else: commission Amazon gives back, its refund
                                            commission, shipping chargebacks, promotions)
              Expenses -> first match wins:
                          Shipping      - shipping to customers (ShippingChargeback, ShippingHB, shipping
                                          promotions) and into Amazon (inbound transportation, partnered
                                          carrier, AWD transportation)
                          Commission    - amount-description 'Commission' (referral fees)
                          FbaFees       - every FBA fee: transaction-type 'FBAFees' (AWD processing and
                                          storage, storage, placement, returns, removals), descriptions
                                          starting 'FBA' (per-unit fulfillment, customer returns, inbound
                                          placement), and the storage/removal/disposal charges listed
                                          under other-transaction
                          OtherExpenses - the rest (advertising, subscriptions, deals, promotions, clawbacks,
                                          deductions, …)
            AdCosts is the advertising Amazon charged ('Cost of Advertising', less any 'Refund for
            Advertiser'). It is part of OtherExpenses, never an amount of its own.
            SalePrice is the price per item: the Principal of the line's order item (settlement, order,
            order-item-code, SKU, transaction type) divided by its quantity-purchased. Without a quantity
            (refund lines, or lines imported before it was kept) it is the whole Principal. Orders and
            refunds are summed apart so a refund does not cancel the sale it reverses. It is NULL for
            lines with no SKU or no Principal (e.g. service fees).
            Units is the quantity on the order item's Principal line and 0 on every other line, so
            summing it counts each order item once. Refund lines carry no quantity, so refunds never
            reduce Units.
            HomeCurrency/ExchangeRate come from rpt.vw_SettlementRate; each Home column is its amount
            times the rate, rounded to the cent (NULL while no rate is known). Single converted lines can
            differ from a converted total by a cent of rounding.
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
        b.Bucket,
        c.Category,
        CASE WHEN b.Bucket = N'Expenses' AND l.AmountType IN (N'Cost of Advertising', N'Refund for Advertiser')
             THEN 1 ELSE 0 END AS IsAdCost,
        p.IsPrincipal,
        CASE WHEN p.IsPrincipal = 1 AND l.TransactionType = N'Order' THEN ISNULL(l.Quantity, 0) ELSE 0 END AS Units,
        SUM(CASE WHEN p.IsPrincipal = 1 THEN l.Amount END)
            OVER (PARTITION BY l.SettlementId, ISNULL(l.AmazonOrderId, N''), ISNULL(l.OrderItemCode, N''), l.Sku, l.TransactionType) AS ItemPrincipal,
        SUM(CASE WHEN p.IsPrincipal = 1 THEN l.Quantity END)
            OVER (PARTITION BY l.SettlementId, ISNULL(l.AmazonOrderId, N''), ISNULL(l.OrderItemCode, N''), l.Sku, l.TransactionType) AS ItemQuantity
    FROM core.Settlement AS s
    INNER JOIN core.SettlementLine AS l ON l.SettlementId = s.SettlementId
    INNER JOIN rpt.vw_SettlementRate AS r ON r.SettlementId = s.SettlementId
    CROSS APPLY (
        SELECT CASE
            WHEN l.AmountDescription IN (N'Previous Reserve Amount Balance', N'Current Reserve Amount') THEN N'Reserve'
            WHEN l.TransactionType LIKE N'Refund%' THEN N'Refunds'
            WHEN l.TransactionType LIKE N'Order%' AND l.AmountType IN (N'ItemPrice', N'ItemWithheldTax') THEN N'Sales'
            WHEN l.AmountType = N'FBA Inventory Reimbursement' AND l.Amount > 0 THEN N'Sales'
            ELSE N'Expenses'
        END AS Bucket
    ) AS b
    CROSS APPLY (
        SELECT CASE
            WHEN b.Bucket = N'Refunds' AND l.AmountType IN (N'ItemPrice', N'ItemWithheldTax') THEN N'RefundedSales'
            WHEN b.Bucket = N'Refunds' THEN N'RefundedExpenses'
            WHEN b.Bucket <> N'Expenses' THEN b.Bucket
            WHEN l.AmountDescription IN (N'ShippingChargeback', N'ShippingHB', N'Inbound Transportation Fee')
              OR l.AmountType IN (N'Inbound Transportation Fee', N'FBA Amazon-Partnered Carrier Shipment Fee', N'AWD Transportation Fee')
              OR (l.AmountType = N'Promotion' AND l.AmountDescription = N'Shipping') THEN N'Shipping'
            WHEN l.AmountDescription = N'Commission' THEN N'Commission'
            WHEN l.TransactionType = N'FBAFees'
              OR l.AmountDescription LIKE N'FBA%'
              OR l.AmountDescription IN (N'Storage Fee', N'StorageRenewalBilling', N'RemovalComplete', N'DisposalComplete') THEN N'FbaFees'
            ELSE N'OtherExpenses'
        END AS Category
    ) AS c
    CROSS APPLY (
        SELECT CASE WHEN l.AmountType = N'ItemPrice' AND l.AmountDescription = N'Principal' THEN 1 ELSE 0 END AS IsPrincipal
    ) AS p
),
Priced AS (
    SELECT
        *,
        CAST(CASE WHEN Sku IS NULL OR ItemPrincipal IS NULL THEN NULL
                  ELSE ABS(ItemPrincipal) / CASE WHEN ItemQuantity > 0 THEN ItemQuantity ELSE 1 END END AS decimal(18, 2)) AS SalePrice
    FROM Lines
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
FROM Priced;
