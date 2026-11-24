/*
  backfill.sql  (V024)
  Purpose : Fills the settlement summary tables for every settlement already in core.Settlement, one
            settlement per call so each commits on its own and the transaction log stays small.
  Notes   : Repeatable: it recalculates every settlement whatever is already there.
*/
SET NOCOUNT ON;

DECLARE @Id nvarchar(32) = N'';
WHILE 1 = 1
BEGIN
    SELECT @Id = MIN(SettlementId) FROM core.Settlement WHERE SettlementId > @Id;
    IF @Id IS NULL
        BREAK;
    EXEC rpt.usp_RefreshSettlementTotals @SettlementId = @Id;
END;
