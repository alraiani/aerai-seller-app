/*
  stg.ufn_NormalizeInventoryState  (V006: keeps inbound stages and reserved reasons separate)
  Purpose : Maps the many state labels used by inventory files onto the canonical states stored in
            core.InventorySnapshot. Stage-specific labels keep their stage (InboundShipped,
            ReservedFcTransfer, ...); plain "inbound"/"reserved" stay unsplit.
  Inputs  : @State - raw state text; case, spaces, hyphens, and underscores are ignored.
  Returns : The canonical state, or NULL when the label is not recognized (promotion rejects the row).
  Notes   : Must stay in sync with AERai.Web.Domain.Core.InventoryStates.
*/
CREATE OR ALTER FUNCTION stg.ufn_NormalizeInventoryState (@State nvarchar(400))
RETURNS nvarchar(32)
WITH SCHEMABINDING
AS
BEGIN
    DECLARE @Key nvarchar(400) = LOWER(REPLACE(REPLACE(REPLACE(@State, N' ', N''), N'-', N''), N'_', N''));

    RETURN CASE
        WHEN @Key IN (N'available', N'fulfillable', N'sellable') THEN N'Available'
        WHEN @Key IN (N'inboundworking', N'working') THEN N'InboundWorking'
        WHEN @Key IN (N'inboundshipped', N'shipped', N'intransit') THEN N'InboundShipped'
        WHEN @Key IN (N'inboundreceiving', N'receiving') THEN N'InboundReceiving'
        WHEN @Key = N'inbound' THEN N'Inbound'
        WHEN @Key IN (N'customerorder', N'customerorders', N'reservedcustomerorders') THEN N'ReservedCustomerOrder'
        WHEN @Key IN (N'fctransfer', N'fctransfers', N'reservedfctransfers') THEN N'ReservedFcTransfer'
        WHEN @Key IN (N'fcprocessing', N'reservedfcprocessing') THEN N'ReservedFcProcessing'
        WHEN @Key = N'reserved' THEN N'Reserved'
        WHEN @Key IN (N'unfulfillable', N'unsellable', N'damaged') THEN N'Unfulfillable'
        ELSE NULL
    END;
END;
