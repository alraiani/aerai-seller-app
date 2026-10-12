/*
  stg.ufn_NormalizeInventoryState
  Purpose : Maps the many state labels used by inventory reports onto the four canonical states
            stored in core.InventorySnapshot (Available, Inbound, Reserved, Unfulfillable).
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
        WHEN @Key IN (N'inbound', N'inboundworking', N'inboundshipped', N'inboundreceiving') THEN N'Inbound'
        WHEN @Key IN (N'reserved', N'fctransfer', N'fcprocessing', N'customerorder') THEN N'Reserved'
        WHEN @Key IN (N'unfulfillable', N'unsellable', N'damaged') THEN N'Unfulfillable'
        ELSE NULL
    END;
END;
