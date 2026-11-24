/*
  Settlement summary tables  (V024)
  Purpose : The settlement pages' numbers, worked out once when a settlement is promoted instead of on
            every page read. Before V024 rpt.vw_SettlementLine ran two window functions and the bucket and
            category rules over every settlement line in the database on each query, and the list page
            queried it about four times per load, which timed out on the live database.
  Tables  : rpt.SettlementLineCalc  - one row per core.SettlementLine: its bucket, category, ad-cost flag,
                                      units, and price per item (the window-function result).
            rpt.SettlementTotals    - one row per core.Settlement: its as-paid column totals.
            rpt.SettlementSkuTotals - one row per settlement, SKU, and price per item: its as-paid totals,
                                      order count, and line count.
  Writer  : rpt.usp_RefreshSettlementTotals only, which core.usp_PromoteImportBatch calls for every
            settlement it promotes. Any new writer of core.SettlementLine must call it too.
  Notes   : Amounts are as paid. Conversion into the marketplace's currency stays in the rpt views
            (rpt.vw_SettlementRate), so a new or adjusted exchange rate needs no refresh.
            Rows cascade away with their line or settlement.
*/
CREATE TABLE rpt.SettlementLineCalc (
    LineId bigint NOT NULL,
    SettlementId nvarchar(32) NOT NULL,
    Bucket nvarchar(16) NOT NULL,
    Category nvarchar(16) NOT NULL,
    IsAdCost bit NOT NULL,
    Units int NOT NULL,
    SalePrice decimal(18, 2) NULL,
    CONSTRAINT PK_SettlementLineCalc PRIMARY KEY (LineId),
    CONSTRAINT FK_SettlementLineCalc_SettlementLine FOREIGN KEY (LineId)
        REFERENCES core.SettlementLine (Id) ON DELETE CASCADE
);

CREATE INDEX IX_SettlementLineCalc_SettlementId
    ON rpt.SettlementLineCalc (SettlementId)
    INCLUDE (Bucket, Category, IsAdCost, Units, SalePrice);

CREATE TABLE rpt.SettlementTotals (
    SettlementId nvarchar(32) NOT NULL,
    Units int NOT NULL,
    Sales decimal(18, 2) NOT NULL,
    Refunds decimal(18, 2) NOT NULL,
    Expenses decimal(18, 2) NOT NULL,
    Reserve decimal(18, 2) NOT NULL,
    RefundedSales decimal(18, 2) NOT NULL,
    RefundedExpenses decimal(18, 2) NOT NULL,
    Shipping decimal(18, 2) NOT NULL,
    FbaFees decimal(18, 2) NOT NULL,
    Commission decimal(18, 2) NOT NULL,
    OtherExpenses decimal(18, 2) NOT NULL,
    AdCosts decimal(18, 2) NOT NULL,
    Net decimal(18, 2) NOT NULL,
    CONSTRAINT PK_SettlementTotals PRIMARY KEY (SettlementId),
    CONSTRAINT FK_SettlementTotals_Settlement FOREIGN KEY (SettlementId)
        REFERENCES core.Settlement (SettlementId) ON DELETE CASCADE
);

CREATE TABLE rpt.SettlementSkuTotals (
    Id bigint IDENTITY(1, 1) NOT NULL,
    SettlementId nvarchar(32) NOT NULL,
    Sku nvarchar(64) NULL,
    SalePrice decimal(18, 2) NULL,
    Units int NOT NULL,
    OrderCount int NOT NULL,
    LineCount int NOT NULL,
    Sales decimal(18, 2) NOT NULL,
    Refunds decimal(18, 2) NOT NULL,
    Expenses decimal(18, 2) NOT NULL,
    Reserve decimal(18, 2) NOT NULL,
    RefundedSales decimal(18, 2) NOT NULL,
    RefundedExpenses decimal(18, 2) NOT NULL,
    Shipping decimal(18, 2) NOT NULL,
    FbaFees decimal(18, 2) NOT NULL,
    Commission decimal(18, 2) NOT NULL,
    OtherExpenses decimal(18, 2) NOT NULL,
    Net decimal(18, 2) NOT NULL,
    CONSTRAINT PK_SettlementSkuTotals PRIMARY KEY NONCLUSTERED (Id),
    CONSTRAINT FK_SettlementSkuTotals_Settlement FOREIGN KEY (SettlementId)
        REFERENCES core.Settlement (SettlementId) ON DELETE CASCADE
);

-- The pages always read one settlement's groups (Summary, Lines), so they are stored together.
CREATE CLUSTERED INDEX IX_SettlementSkuTotals_Settlement
    ON rpt.SettlementSkuTotals (SettlementId, Sku, SalePrice);
