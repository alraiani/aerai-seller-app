/*
  fix-correction-balances  (V020)
  Purpose      : Makes ledger rows edited before the fix show the home-stock count after the edit. An edit
                 (a correction: the entry is reversed and a corrected copy logged) writes the reversal first
                 when it can; when that would dip below zero, the copy is written first. Before the fix, such a
                 copy kept the in-between balance (e.g. 92 instead of 42), and the ledger lists show the copy
                 while hiding the reversal.
  Grain        : One correction = the copy (CorrectsId = X) and the reversal of the same entry (ReversesId = X).
  Inputs       : core.HomeStockMovement.
  Side effects : Swaps BalanceAfter between the copy and its reversal, as the app now does when it writes the
                 copy first. Units and core.HomeStock are untouched (the counts were always right).
  Idempotency  : A pair is only swapped while the reversal still holds the count after the copy plus its own
                 units (copy written first, balances not yet swapped). After the swap that no longer holds
                 (units are never zero), so running it again, or on a database without such edits, does nothing.
*/
SET NOCOUNT ON;

DECLARE @pairs TABLE (CopyId bigint PRIMARY KEY, ReversalId bigint NOT NULL, CopyBalance int NOT NULL, ReversalBalance int NOT NULL);

INSERT @pairs (CopyId, ReversalId, CopyBalance, ReversalBalance)
SELECT c.Id, r.Id, c.BalanceAfter, r.BalanceAfter
FROM core.HomeStockMovement AS c
INNER JOIN core.HomeStockMovement AS r
    ON r.ReversesId = c.CorrectsId
   AND r.MarketplaceId = c.MarketplaceId
   AND r.Sku = c.Sku
WHERE c.CorrectsId IS NOT NULL
  AND c.Id < r.Id
  AND r.BalanceAfter = c.BalanceAfter + r.Units;

UPDATE m SET BalanceAfter = p.ReversalBalance
FROM core.HomeStockMovement AS m
INNER JOIN @pairs AS p ON p.CopyId = m.Id;

UPDATE m SET BalanceAfter = p.CopyBalance
FROM core.HomeStockMovement AS m
INNER JOIN @pairs AS p ON p.ReversalId = m.Id;
