/*
  backfill-deposit-date  (V017)
  Purpose      : Gives settlements imported before V017 their deposit date, read from the header row kept
                 in staging, so their exchange rate can be taken for the day they were paid.
  Inputs       : stg.SettlementLine.RawLine of header rows (no transaction type).
  Side effects : Sets stg.SettlementLine.DepositDate on those header rows and core.Settlement.DepositDate
                 from the latest batch of each settlement.
  Idempotency  : Only empty values are filled, so running it again does nothing.
  Notes        : deposit-date is field 4 of the settlement flat file V2 (settlement-id, settlement-start-date,
                 settlement-end-date, deposit-date, total-amount, currency). A row is only read when its
                 settlement-id (field 1) matches what was staged from the same line, so a file with another
                 layout is left alone. Dates are parsed with the same function promotion uses.
*/
SET NOCOUNT ON;

UPDATE l
SET DepositDate = NULLIF(LTRIM(RTRIM(p.DepositDate)), N'')
FROM stg.SettlementLine AS l
INNER JOIN (
    SELECT
        r.Id,
        MAX(CASE WHEN f.ordinal = 1 THEN f.value END) AS SettlementId,
        MAX(CASE WHEN f.ordinal = 4 THEN f.value END) AS DepositDate
    FROM stg.SettlementLine AS r
    CROSS APPLY STRING_SPLIT(r.RawLine, CHAR(9), 1) AS f
    WHERE r.TransactionType IS NULL AND r.DepositDate IS NULL
    GROUP BY r.Id
) AS p ON p.Id = l.Id
WHERE ISNULL(p.SettlementId, N'') = ISNULL(l.SettlementId, N'');

UPDATE s
SET DepositDate = d.DepositDate
FROM core.Settlement AS s
CROSS APPLY (
    SELECT MAX(stg.ufn_TryParseDateTimeOffset(l.DepositDate)) AS DepositDate
    FROM stg.SettlementLine AS l
    WHERE l.ImportBatchId = s.LastImportBatchId AND l.SettlementId = s.SettlementId
) AS d
WHERE s.DepositDate IS NULL AND d.DepositDate IS NOT NULL;
