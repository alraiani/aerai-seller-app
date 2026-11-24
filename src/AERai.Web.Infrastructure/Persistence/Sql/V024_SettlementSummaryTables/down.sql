/*
  down.sql  (V024)
  Purpose : Removes the settlement summary tables and their refresh proc. Run after the V021 views and the
            V022 promotion proc are back, since the V024 versions read these objects.
*/
DROP PROCEDURE IF EXISTS rpt.usp_RefreshSettlementTotals;
DROP TABLE IF EXISTS rpt.SettlementSkuTotals;
DROP TABLE IF EXISTS rpt.SettlementTotals;
DROP TABLE IF EXISTS rpt.SettlementLineCalc;
