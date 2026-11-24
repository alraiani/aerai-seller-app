/*
  rpt.usp_RefreshSettlementTotals  (V024)
  Purpose      : Recalculates one settlement's rows in the settlement summary tables from its lines. The
                 single definition of the Sales/Refunds/Expenses/Reserve rule and of the categories inside
                 it (moved here from rpt.vw_SettlementLine, unchanged), so the settlement list, the per-SKU
                 summary, and the line drill-down always add up to the same payout, and each column matches
                 the one Seller Central shows on the settlement's statement.
  Inputs       : @SettlementId - core.Settlement.SettlementId.
  Side effects : Replaces the settlement's rows in rpt.SettlementLineCalc, rpt.SettlementSkuTotals, and
                 rpt.SettlementTotals (removes them when the settlement no longer exists). Runs inside the
                 caller's transaction when there is one (core.usp_PromoteImportBatch).
  Idempotency  : Safe to run repeatedly; the rows always converge to what the lines say.
  Notes        : Buckets, in priority order (checked against Seller Central's statements to the cent):
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
                               FbaFees       - every FBA fee: transaction-type 'FBAFees', descriptions
                                               starting 'FBA', and the storage/removal/disposal charges
                                               listed under other-transaction
                               OtherExpenses - the rest (advertising, subscriptions, deals, promotions,
                                               clawbacks, deductions, …)
                 IsAdCost marks the advertising Amazon charged ('Cost of Advertising', less any 'Refund for
                 Advertiser'); it is part of OtherExpenses, never an amount of its own.
                 SalePrice is the price per item: the Principal of the line's order item (settlement, order,
                 order-item-code, SKU, transaction type) divided by its quantity-purchased; without a
                 quantity it is the whole Principal. NULL for lines with no SKU or no Principal.
                 Units is the quantity on the order item's Principal line and 0 on every other line, so
                 summing it counts each order item once; refunds never reduce it.
*/
CREATE OR ALTER PROCEDURE rpt.usp_RefreshSettlementTotals
    @SettlementId nvarchar(32)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DELETE FROM rpt.SettlementLineCalc WHERE SettlementId = @SettlementId;
    DELETE FROM rpt.SettlementSkuTotals WHERE SettlementId = @SettlementId;
    DELETE FROM rpt.SettlementTotals WHERE SettlementId = @SettlementId;

    WITH Lines AS (
        SELECT
            l.Id AS LineId,
            l.SettlementId,
            l.Sku,
            b.Bucket,
            c.Category,
            CASE WHEN b.Bucket = N'Expenses' AND l.AmountType IN (N'Cost of Advertising', N'Refund for Advertiser')
                 THEN 1 ELSE 0 END AS IsAdCost,
            CASE WHEN p.IsPrincipal = 1 AND l.TransactionType = N'Order' THEN ISNULL(l.Quantity, 0) ELSE 0 END AS Units,
            SUM(CASE WHEN p.IsPrincipal = 1 THEN l.Amount END)
                OVER (PARTITION BY ISNULL(l.AmazonOrderId, N''), ISNULL(l.OrderItemCode, N''), l.Sku, l.TransactionType) AS ItemPrincipal,
            SUM(CASE WHEN p.IsPrincipal = 1 THEN l.Quantity END)
                OVER (PARTITION BY ISNULL(l.AmazonOrderId, N''), ISNULL(l.OrderItemCode, N''), l.Sku, l.TransactionType) AS ItemQuantity
        FROM core.SettlementLine AS l
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
        WHERE l.SettlementId = @SettlementId
    )
    INSERT rpt.SettlementLineCalc (LineId, SettlementId, Bucket, Category, IsAdCost, Units, SalePrice)
    SELECT
        LineId,
        SettlementId,
        Bucket,
        Category,
        IsAdCost,
        Units,
        CAST(CASE WHEN Sku IS NULL OR ItemPrincipal IS NULL THEN NULL
                  ELSE ABS(ItemPrincipal) / CASE WHEN ItemQuantity > 0 THEN ItemQuantity ELSE 1 END END AS decimal(18, 2))
    FROM Lines;

    INSERT rpt.SettlementSkuTotals
        (SettlementId, Sku, SalePrice, Units, OrderCount, LineCount, Sales, Refunds, Expenses, Reserve,
         RefundedSales, RefundedExpenses, Shipping, FbaFees, Commission, OtherExpenses, Net)
    SELECT
        l.SettlementId,
        l.Sku,
        c.SalePrice,
        SUM(c.Units),
        COUNT(DISTINCT l.AmazonOrderId),
        COUNT(*),
        SUM(CASE WHEN c.Bucket = N'Sales' THEN l.Amount ELSE 0 END),
        SUM(CASE WHEN c.Bucket = N'Refunds' THEN l.Amount ELSE 0 END),
        SUM(CASE WHEN c.Bucket = N'Expenses' THEN l.Amount ELSE 0 END),
        SUM(CASE WHEN c.Bucket = N'Reserve' THEN l.Amount ELSE 0 END),
        SUM(CASE WHEN c.Category = N'RefundedSales' THEN l.Amount ELSE 0 END),
        SUM(CASE WHEN c.Category = N'RefundedExpenses' THEN l.Amount ELSE 0 END),
        SUM(CASE WHEN c.Category = N'Shipping' THEN l.Amount ELSE 0 END),
        SUM(CASE WHEN c.Category = N'FbaFees' THEN l.Amount ELSE 0 END),
        SUM(CASE WHEN c.Category = N'Commission' THEN l.Amount ELSE 0 END),
        SUM(CASE WHEN c.Category = N'OtherExpenses' THEN l.Amount ELSE 0 END),
        SUM(l.Amount)
    FROM core.SettlementLine AS l
    INNER JOIN rpt.SettlementLineCalc AS c ON c.LineId = l.Id
    WHERE l.SettlementId = @SettlementId
    GROUP BY l.SettlementId, l.Sku, c.SalePrice;

    -- Every settlement gets a totals row (zeros when it has no lines), as the summary always listed it.
    INSERT rpt.SettlementTotals
        (SettlementId, Units, Sales, Refunds, Expenses, Reserve, RefundedSales, RefundedExpenses,
         Shipping, FbaFees, Commission, OtherExpenses, AdCosts, Net)
    SELECT
        s.SettlementId,
        ISNULL(SUM(c.Units), 0),
        ISNULL(SUM(CASE WHEN c.Bucket = N'Sales' THEN l.Amount ELSE 0 END), 0),
        ISNULL(SUM(CASE WHEN c.Bucket = N'Refunds' THEN l.Amount ELSE 0 END), 0),
        ISNULL(SUM(CASE WHEN c.Bucket = N'Expenses' THEN l.Amount ELSE 0 END), 0),
        ISNULL(SUM(CASE WHEN c.Bucket = N'Reserve' THEN l.Amount ELSE 0 END), 0),
        ISNULL(SUM(CASE WHEN c.Category = N'RefundedSales' THEN l.Amount ELSE 0 END), 0),
        ISNULL(SUM(CASE WHEN c.Category = N'RefundedExpenses' THEN l.Amount ELSE 0 END), 0),
        ISNULL(SUM(CASE WHEN c.Category = N'Shipping' THEN l.Amount ELSE 0 END), 0),
        ISNULL(SUM(CASE WHEN c.Category = N'FbaFees' THEN l.Amount ELSE 0 END), 0),
        ISNULL(SUM(CASE WHEN c.Category = N'Commission' THEN l.Amount ELSE 0 END), 0),
        ISNULL(SUM(CASE WHEN c.Category = N'OtherExpenses' THEN l.Amount ELSE 0 END), 0),
        ISNULL(SUM(CASE WHEN c.IsAdCost = 1 THEN l.Amount ELSE 0 END), 0),
        ISNULL(SUM(l.Amount), 0)
    FROM core.Settlement AS s
    LEFT JOIN core.SettlementLine AS l ON l.SettlementId = s.SettlementId
    LEFT JOIN rpt.SettlementLineCalc AS c ON c.LineId = l.Id
    WHERE s.SettlementId = @SettlementId
    GROUP BY s.SettlementId;

    COMMIT TRANSACTION;
END;
